using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.Extensions.Primitives;

namespace Broca.ActivityPub.Server.Controllers;

[ApiController]
[Route("users/{username}/inbox")]
public class InboxController : ActivityPubControllerBase
{
    private readonly IInboxHandler _inboxHandler;
    private readonly IActivityRepository _activityRepository;
    private readonly IActorRepository _actorRepository;
    private readonly IHttpSignatureVerifier _signatureVerifier;
    private readonly LinkedDataSignatureVerifier _ldSignatureVerifier;
    private readonly SignedClientProvider _signedClientProvider;
    private readonly AttachmentProcessingService _attachmentProcessingService;
    private readonly ObjectEnrichmentService _enrichmentService;
    private readonly IMemoryCache _cache;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<InboxController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private static readonly TimeSpan PublicKeyCacheDuration = TimeSpan.FromHours(1);

    public InboxController(
        IInboxHandler inboxHandler,
        IActivityRepository activityRepository,
        IActorRepository actorRepository,
        IHttpSignatureVerifier signatureVerifier,
        LinkedDataSignatureVerifier ldSignatureVerifier,
        SignedClientProvider signedClientProvider,
        AttachmentProcessingService attachmentProcessingService,
        ObjectEnrichmentService enrichmentService,
        IMemoryCache cache,
        IOptions<ActivityPubServerOptions> options,
        ILogger<InboxController> logger)
    {
        _inboxHandler = inboxHandler;
        _activityRepository = activityRepository;
        _actorRepository = actorRepository;
        _signatureVerifier = signatureVerifier;
        _ldSignatureVerifier = ldSignatureVerifier;
        _signedClientProvider = signedClientProvider;
        _attachmentProcessingService = attachmentProcessingService;
        _enrichmentService = enrichmentService;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    [HttpGet]
    [Produces("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> Get(string username, [FromQuery] int page = 0, [FromQuery] int limit = 20)
    {
        try
        {
            // Verify actor exists
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            var search = GetSearchParameters();
            var offset = page * limit;
            var baseUrl = GetBaseUrl(_options.NormalizedRoutePrefix);

            IEnumerable<IObjectOrLink> activities;
            int totalCount;
            bool itemsAlreadyPaginated;

            if (search?.HasSearchCriteria == true && _activityRepository is ISearchableActivityRepository searchableRepo)
            {
                activities = await searchableRepo.GetInboxActivitiesAsync(username, search, limit, offset);
                totalCount = await searchableRepo.GetInboxCountAsync(username, search);
                itemsAlreadyPaginated = true;
            }
            else if (search?.HasSearchCriteria == true)
            {
                activities = await _activityRepository.GetInboxActivitiesAsync(username, int.MaxValue, 0);
                totalCount = activities.Count();
                itemsAlreadyPaginated = false;
            }
            else
            {
                activities = await _activityRepository.GetInboxActivitiesAsync(username, limit, offset);
                totalCount = await _activityRepository.GetInboxCountAsync(username);
                itemsAlreadyPaginated = true;
            }

            // Rewrite attachment URLs and enrich with collection metadata
            foreach (var activity in activities)
            {
                if (activity is IObject obj)
                {
                    await _attachmentProcessingService.RewriteAttachmentUrlsAsync(obj, username);
                }
                
                // Enrich activities with collection information (replies, likes, shares counts)
                await _enrichmentService.EnrichActivityAsync(activity, baseUrl);
            }

            var collectionUrl = $"{baseUrl}/users/{username}/inbox";
            return BuildCollectionResponse(collectionUrl, activities, totalCount, page, limit, search, itemsAlreadyPaginated);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { error = $"Invalid search parameter: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inbox for {Username}", username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost]
    [Consumes("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> Post(string username)
    {
        try
        {
            // Verify actor exists
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            // Read and parse the activity
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            
            if (string.IsNullOrWhiteSpace(body))
            {
                return BadRequest(new { error = "Empty request body" });
            }

            IObjectOrLink? activity;
            try
            {
                activity = JsonSerializer.Deserialize<IObjectOrLink>(body, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON in inbox request for {Username}", username);
                return BadRequest(new { error = "Invalid JSON" });
            }

            if (activity == null)
            {
                return BadRequest(new { error = "Failed to parse activity" });
            }

            // Check if this is an admin operation to system actor with bearer token
            bool isAuthenticatedAdmin = false;
            if (username == _options.SystemActorUsername && _options.EnableAdminOperations)
            {
                isAuthenticatedAdmin = IsAdminTokenValid();
                if (isAuthenticatedAdmin)
                {
                    _logger.LogInformation("Admin operation authenticated via bearer token for system actor");
                }
            }

            // Verify HTTP signature if required (skip for authenticated admin operations)
            if (_options.RequireHttpSignatures && !isAuthenticatedAdmin)
            {
                _logger.LogInformation("HTTP signature verification required for inbox request to {Username}", username);

                var isDelete = IsDeleteActivity(body, out var deleteActorId, out var deleteObjectId);

                bool signatureValid = false;
                try
                {
                    signatureValid = await VerifySignatureAsync(body, HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    if (!isDelete) throw;
                    _logger.LogWarning("HTTP signature verification failed for Delete activity: {Error}", ex.Message);
                }

                if (!signatureValid && isDelete)
                {
                    signatureValid = await TryVerifyDeleteAsync(body, deleteActorId, deleteObjectId, HttpContext.RequestAborted);
                }

                if (!signatureValid)
                {
                    _logger.LogWarning("Signature verification failed for inbox request to {Username}", username);
                    return Unauthorized(new { error = "Invalid signature" });
                }

                _logger.LogInformation("Signature verified for inbox request to {Username}", username);
            }
            else
            {
                _logger.LogWarning("HTTP signatures not required - accepting request without verification (not recommended for production)");
            }

            // Process the activity
            var success = await _inboxHandler.HandleActivityAsync(username, activity, isAuthenticatedAdmin, HttpContext.RequestAborted);

            if (success)
            {
                return Accepted();
            }
            else
            {
                return BadRequest(new { error = "Failed to process activity" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing inbox POST for {Username}", username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Returns true when the body is a Delete activity and outputs the actor/object IDs.
    /// </summary>
    private static bool IsDeleteActivity(string body, out string? actorId, out string? objectId)
    {
        actorId = null;
        objectId = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "Delete")
                return false;
            if (root.TryGetProperty("actor", out var actor))
                actorId = actor.ValueKind == JsonValueKind.String ? actor.GetString() : null;
            if (root.TryGetProperty("object", out var obj))
                objectId = obj.ValueKind == JsonValueKind.String ? obj.GetString()
                    : obj.TryGetProperty("id", out var id) ? id.GetString() : null;
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Fallback verification for Delete activities when HTTP signature verification fails.
    /// Tries Linked Data Signature (body-embedded), then domain trust as a last resort.
    /// </summary>
    private async Task<bool> TryVerifyDeleteAsync(
        string body, string? actorId, string? objectId, CancellationToken cancellationToken)
    {
        // --- LD Signature ---
        if (_ldSignatureVerifier.TryGetSignatureCreator(body, out var creatorUri) && !string.IsNullOrEmpty(creatorUri))
        {
            _logger.LogInformation("Attempting LD signature verification for Delete activity, creator={Creator}", creatorUri);
            var publicKeyPem = await FetchActorPublicKeyAsync(creatorUri.Split('#')[0], cancellationToken);
            if (!string.IsNullOrEmpty(publicKeyPem))
            {
                if (_ldSignatureVerifier.Verify(body, publicKeyPem))
                {
                    _logger.LogInformation("Delete activity accepted via LD signature");
                    return true;
                }
                _logger.LogWarning("LD signature verification failed for Delete activity");
            }
            else
            {
                _logger.LogWarning("Could not fetch public key for LD signature creator {Creator}", creatorUri);
            }
        }

        // --- Domain trust ---
        // When we cannot verify cryptographically (actor/key is already gone), accept deletes
        // where the signing key domain matches the actor URI domain — the server is asserting
        // authority over its own actors.
        var signingDomain = GetSigningKeyDomain();
        if (!string.IsNullOrEmpty(actorId) && !string.IsNullOrEmpty(signingDomain))
        {
            if (Uri.TryCreate(actorId, UriKind.Absolute, out var actorUri) &&
                string.Equals(actorUri.Host, signingDomain, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Delete activity accepted via domain trust (actor domain={Domain} matches signing key domain)",
                    signingDomain);
                return true;
            }
        }

        _logger.LogWarning(
            "Delete activity rejected: LD sig failed and domain trust did not apply (actorId={ActorId}, signingDomain={Domain})",
            actorId, signingDomain);
        return false;
    }

    private string? GetSigningKeyDomain()
    {
        try
        {
            if (!Request.Headers.TryGetValue("Signature", out var sigHeader))
                return null;
            var keyId = _signatureVerifier.GetSignatureKeyId(sigHeader.ToString());
            if (Uri.TryCreate(keyId, UriKind.Absolute, out var keyUri))
                return keyUri.Host;
        }
        catch { /* best effort */ }
        return null;
    }

    /// <summary>
    /// Verifies the HTTP signature of an incoming request
    /// </summary>
    private async Task<bool> VerifySignatureAsync(string body, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting signature verification. Request headers: {Headers}", 
            string.Join(", ", Request.Headers.Keys));
        
        if (!Request.Headers.TryGetValue("Signature", out var signatureHeader) || string.IsNullOrEmpty(signatureHeader))
        {
            _logger.LogWarning("Signature header is missing from request");
            throw new InvalidOperationException("Signature header is missing");
        }

        _logger.LogDebug("Signature header found: {SignatureHeader}", signatureHeader!);
        
        var keyId = _signatureVerifier.GetSignatureKeyId(signatureHeader!);
        _logger.LogInformation("Extracted keyId from signature: {KeyId}", keyId);

        ValidateRequestClockSkew(Request);
        
        var publicKeyPem = await FetchActorPublicKeyAsync(keyId, cancellationToken);
        
        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            _logger.LogWarning("Could not retrieve public key for keyId: {KeyId}", keyId);
            throw new InvalidOperationException($"Could not retrieve public key for keyId: {keyId}");
        }

        _logger.LogDebug("Successfully fetched public key for keyId: {KeyId}", keyId);

        var headers = new Dictionary<string, string>();
        headers["signature"] = signatureHeader!;
        headers["(request-target)"] = $"{Request.Method.ToLower()} {Request.Path}";

        foreach (var header in Request.Headers)
        {
            var headerName = header.Key.ToLower();
            if (headerName != "signature")
                headers[headerName] = header.Value.ToString();
        }
        
        _logger.LogDebug("Headers being verified: {Headers}", 
            string.Join(", ", headers.Select(h => $"\"{h.Key}\"")));

        if (Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            if (!Request.Headers.TryGetValue("Digest", out var digestHeader))
            {
                _logger.LogWarning("POST request missing Digest header");
            }
            else
            {
                var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);
                if (!_signatureVerifier.VerifyDigest(bodyBytes, digestHeader.ToString()))
                {
                    _logger.LogWarning("Digest header mismatch for inbox request to keyId: {KeyId}", keyId);
                    throw new InvalidOperationException("Digest header does not match request body");
                }
            }
        }

        _logger.LogDebug("Calling IHttpSignatureVerifier.VerifyAsync with {HeaderCount} headers", headers.Count);
        var result = await _signatureVerifier.VerifyAsync(headers, publicKeyPem, cancellationToken);
        _logger.LogDebug("Signature verification result: {Result}", result);
        return result;
    }

    /// <summary>
    /// Fetches an actor's public key from their profile using the ActivityPub client
    /// </summary>
    /// <remarks>
    /// Uses a memory cache to reduce network requests. The cache key is the keyId.
    /// Public keys are cached for 1 hour by default.
    /// Checks local repository first before fetching from remote servers.
    /// </remarks>
    private async Task<string?> FetchActorPublicKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        try
        {
            // Check cache first
            var cacheKey = $"publickey:{keyId}";
            if (_cache.TryGetValue<string>(cacheKey, out var cachedKey))
            {
                _logger.LogDebug("Using cached public key for {KeyId}", keyId);
                return cachedKey;
            }

            _logger.LogDebug("Public key not in cache for {KeyId}", keyId);
            
            // Extract the actor URL from the keyId (e.g., https://example.com/users/alice#main-key -> https://example.com/users/alice)
            var actorUrl = keyId.Split('#')[0];
            _logger.LogDebug("Extracted actor URL from keyId: {ActorUrl}", actorUrl);
            
            // First check if actor exists in local repository (important for testing and cached remote actors)
            Actor? actor = await _actorRepository.GetActorByIdAsync(actorUrl, cancellationToken);
            
            if (actor == null)
            {
                _logger.LogDebug("Actor not in local repository, fetching from {ActorUrl} via signed GET", actorUrl);
                actor = await FetchActorWithSignatureAsync(actorUrl, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Using actor from local repository for {ActorUrl}", actorUrl);
            }
            
            if (actor == null)
            {
                _logger.LogWarning("Failed to fetch actor from {ActorUrl}", actorUrl);
                return null;
            }

            // Extract publicKeyPem from actor's extension data
            // The publicKey property is typically in the extensions
            string? publicKeyPem = null;
            
            if (actor.ExtensionData != null && actor.ExtensionData.TryGetValue("publicKey", out var publicKeyObj))
            {
                // Handle JsonElement case (most common from deserialization)
                if (publicKeyObj is JsonElement publicKeyElement)
                {
                    // Verify the keyId matches if present
                    if (publicKeyElement.TryGetProperty("id", out var keyIdElement))
                    {
                        var actualKeyId = keyIdElement.GetString();
                        if (actualKeyId != keyId)
                        {
                            _logger.LogWarning("KeyId mismatch. Expected: {Expected}, Got: {Got}", 
                                keyId, actualKeyId);
                        }
                    }

                    // Extract the PEM-encoded public key
                    if (publicKeyElement.TryGetProperty("publicKeyPem", out var pemElement))
                    {
                        publicKeyPem = pemElement.GetString();
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(publicKeyPem))
            {
                _logger.LogWarning("Actor {ActorUrl} does not have a publicKey.publicKeyPem in extension data", actorUrl);
                return null;
            }

            // Cache the public key
            _cache.Set(cacheKey, publicKeyPem, PublicKeyCacheDuration);
            _logger.LogDebug("Cached public key for {KeyId} (expires in {Duration})", keyId, PublicKeyCacheDuration);

            return publicKeyPem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching actor public key from {KeyId}", keyId);
            return null;
        }
    }

    private async Task<Actor?> FetchActorWithSignatureAsync(string actorUrl, CancellationToken cancellationToken)
    {
        try
        {
            var client = await _signedClientProvider.CreateForSystemActorAsync(cancellationToken);
            return await client.GetActorAsync(new Uri(actorUrl), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signed GET for actor {ActorUrl} failed", actorUrl);
            return null;
        }
    }

    /// <summary>
    /// Validates the admin API token from the Authorization header
    /// </summary>
    private bool IsAdminTokenValid()
    {
        // Check if admin token is configured
        if (string.IsNullOrWhiteSpace(_options.AdminApiToken))
        {
            return false;
        }

        // Extract token from Authorization header
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            return false;
        }

        // Check for Bearer token format
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedToken = authHeader.Substring(7).Trim();
        
        // Constant-time comparison to prevent timing attacks (reuse from ActorController)
        if (providedToken == null || _options.AdminApiToken == null)
        {
            return false;
        }

        if (providedToken.Length != _options.AdminApiToken.Length)
        {
            return false;
        }

        int result = 0;
        for (int i = 0; i < providedToken.Length; i++)
        {
            result |= providedToken[i] ^ _options.AdminApiToken[i];
        }

        return result == 0;
    }
}
