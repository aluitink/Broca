using System.Text.Json;
using Broca.ActivityPub.Client.Services;
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
public class InboxController : ControllerBase
{
    private readonly IInboxHandler _inboxHandler;
    private readonly IActivityRepository _activityRepository;
    private readonly IActorRepository _actorRepository;
    private readonly HttpSignatureService _signatureService;
    private readonly IActivityPubClient _activityPubClient;
    private readonly AttachmentProcessingService _attachmentProcessingService;
    private readonly IMemoryCache _cache;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<InboxController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private static readonly TimeSpan PublicKeyCacheDuration = TimeSpan.FromHours(1);

    public InboxController(
        IInboxHandler inboxHandler,
        IActivityRepository activityRepository,
        IActorRepository actorRepository,
        HttpSignatureService signatureService,
        IActivityPubClient activityPubClient,
        AttachmentProcessingService attachmentProcessingService,
        IMemoryCache cache,
        IOptions<ActivityPubServerOptions> options,
        ILogger<InboxController> logger)
    {
        _inboxHandler = inboxHandler;
        _activityRepository = activityRepository;
        _actorRepository = actorRepository;
        _signatureService = signatureService;
        _activityPubClient = activityPubClient;
        _attachmentProcessingService = attachmentProcessingService;
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

            var offset = page * limit;
            var activities = await _activityRepository.GetInboxActivitiesAsync(username, limit, offset);
            
            // Rewrite attachment URLs to use local blob storage
            foreach (var activity in activities)
            {
                if (activity is IObject obj)
                {
                    await _attachmentProcessingService.RewriteAttachmentUrlsAsync(obj, username);
                }
            }
            
            var totalCount = await _activityRepository.GetInboxCountAsync(username);
            var baseUrl = $"{Request.Scheme}://{Request.Host}{_options.NormalizedRoutePrefix}";

            if (page == 0 && limit == 20)
            {
                // Return the collection wrapper when no pagination params or default
                var collection = new OrderedCollection
                {
                    JsonLDContext = new List<ITermDefinition> 
                    { 
                        new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) 
                    },
                    Id = $"{baseUrl}/users/{username}/inbox",
                    TotalItems = (uint)totalCount,
                    First = totalCount > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/inbox?page=0&limit={limit}") } 
                        : null
                };
                return Ok(collection);
            }
            else
            {
                // Return a collection page
                var collectionPage = new OrderedCollectionPage
                {
                    JsonLDContext = new List<ITermDefinition> 
                    { 
                        new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) 
                    },
                    Id = $"{baseUrl}/users/{username}/inbox?page={page}&limit={limit}",
                    PartOf = new Link { Href = new Uri($"{baseUrl}/users/{username}/inbox") },
                    TotalItems = (uint)totalCount,
                    OrderedItems = activities.ToList(),
                    Next = (offset + limit < totalCount) 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/inbox?page={page + 1}&limit={limit}") }
                        : null,
                    Prev = page > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/inbox?page={page - 1}&limit={limit}") }
                        : null
                };
                return Ok(collectionPage);
            }
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
                try
                {
                    var isValid = await VerifySignatureAsync(body, HttpContext.RequestAborted);
                    if (!isValid)
                    {
                        _logger.LogWarning("Invalid HTTP signature for inbox request to {Username}. Signature verification returned false.", username);
                        return Unauthorized(new { error = "Invalid signature" });
                    }
                    _logger.LogInformation("HTTP signature successfully verified for inbox request to {Username}", username);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Signature verification failed for inbox request to {Username}. Error: {ErrorMessage}", username, ex.Message);
                    return Unauthorized(new { error = "Signature verification failed", detail = ex.Message });
                }
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
    /// Verifies the HTTP signature of an incoming request
    /// </summary>
    private async Task<bool> VerifySignatureAsync(string body, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting signature verification. Request headers: {Headers}", 
            string.Join(", ", Request.Headers.Keys));
        
        // Get Signature header
        if (!Request.Headers.TryGetValue("Signature", out var signatureHeader) || string.IsNullOrEmpty(signatureHeader))
        {
            _logger.LogWarning("Signature header is missing from request");
            throw new InvalidOperationException("Signature header is missing");
        }

        _logger.LogDebug("Signature header found: {SignatureHeader}", signatureHeader!);
        
        // Parse the signature to see what headers it expects
        var signatureParts = _signatureService.ParseSignatureParts(signatureHeader!);
        if (signatureParts.TryGetValue("headers", out var headersInSignature))
        {
            _logger.LogInformation("Signature expects these headers to be signed: {SignedHeaders}", headersInSignature);
        }
        
        // Extract keyId from signature
        var keyId = _signatureService.GetSignatureKeyId(signatureHeader!);
        _logger.LogInformation("Extracted keyId from signature: {KeyId}", keyId);
        
        // Fetch the actor's public key
        var publicKeyPem = await FetchActorPublicKeyAsync(keyId, cancellationToken);
        
        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            _logger.LogWarning("Could not retrieve public key for keyId: {KeyId}", keyId);
            throw new InvalidOperationException($"Could not retrieve public key for keyId: {keyId}");
        }

        _logger.LogDebug("Successfully fetched public key for keyId: {KeyId}", keyId);

        // Build headers dictionary for verification
        var headers = new Dictionary<string, string>();
        
        // Add the Signature header itself (required for verification)
        headers["signature"] = signatureHeader!;
        
        // Add (request-target) pseudo-header
        var requestTarget = $"{Request.Method.ToLower()} {Request.Path}";
        headers["(request-target)"] = requestTarget;

        // Add all headers from the request (lowercase keys)
        // The verification service will use only the ones that are part of the signature
        foreach (var header in Request.Headers)
        {
            var headerName = header.Key.ToLower();
            // Don't duplicate the signature header
            if (headerName != "signature")
            {
                headers[headerName] = header.Value.ToString();
            }
        }
        
        _logger.LogDebug("Headers being verified: {Headers}", 
            string.Join(", ", headers.Select(h => $"\"{h.Key}\"")));

        // Validate Digest header for POST requests (required per ActivityPub spec)
        if (Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            if (!Request.Headers.TryGetValue("Digest", out var digestHeader))
            {
                _logger.LogWarning("POST request missing Digest header");
                // Some implementations may not send Digest, log but don't fail
            }
            else
            {
                // Verify the digest matches the body
                var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);
                var expectedDigest = _signatureService.ComputeContentDigestHash(bodyBytes);
                var digestValue = digestHeader.ToString();
                
                if (digestValue.StartsWith("SHA-256="))
                {
                    var providedDigest = digestValue.Substring(8);
                    if (providedDigest != expectedDigest)
                    {
                        _logger.LogWarning("Digest header mismatch. Expected: {Expected}, Got: {Got}", 
                            expectedDigest, providedDigest);
                        throw new InvalidOperationException("Digest header does not match request body");
                    }
                }
            }
        }

        // Verify the signature
        _logger.LogDebug("Calling HttpSignatureService.VerifyHttpSignatureAsync with {HeaderCount} headers", headers.Count);
        var result = await _signatureService.VerifyHttpSignatureAsync(headers, publicKeyPem, cancellationToken);
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
                _logger.LogDebug("Actor not in local repository, fetching from {ActorUrl} via ActivityPub client", actorUrl);
                
                // Use ActivityPubClient to fetch the actor from remote server
                // Note: The client is configured with the system actor credentials for signing outbound requests
                actor = await _activityPubClient.GetActorAsync(new Uri(actorUrl), cancellationToken);
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
