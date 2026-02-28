using System.Text.Json;
using Broca.ActivityPub.Client.Services;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Server.Services;
using KristofferStrube.ActivityStreams;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Server.Controllers;

/// <summary>
/// Shared inbox controller for efficient server-to-server delivery
/// </summary>
/// <remarks>
/// The shared inbox receives activities destined for multiple users on this server.
/// Instead of delivering to each user's individual inbox, remote servers can
/// deliver once to the shared inbox, which then routes to local recipients.
/// This is much more efficient for servers with many followers.
/// </remarks>
[ApiController]
[Route("inbox")]
public class SharedInboxController : ActivityPubControllerBase
{
    private readonly IInboxHandler _inboxHandler;
    private readonly IActorRepository _actorRepository;
    private readonly HttpSignatureService _signatureService;
    private readonly IActivityPubClient _activityPubClient;
    private readonly IMemoryCache _cache;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<SharedInboxController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SharedInboxController(
        IInboxHandler inboxHandler,
        IActorRepository actorRepository,
        HttpSignatureService signatureService,
        IActivityPubClient activityPubClient,
        IMemoryCache cache,
        IOptions<ActivityPubServerOptions> options,
        ILogger<SharedInboxController> logger)
    {
        _inboxHandler = inboxHandler;
        _actorRepository = actorRepository;
        _signatureService = signatureService;
        _activityPubClient = activityPubClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// POST to shared inbox - receives activities for any user on this server
    /// </summary>
    [HttpPost]
    [Consumes("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> Post()
    {
        try
        {
            // Read the request body
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            // Verify HTTP signature
            if (_options.RequireHttpSignatures)
            {
                if (!await VerifySignatureAsync(body, HttpContext.RequestAborted))
                {
                    _logger.LogWarning("Shared inbox POST rejected: invalid signature");
                    return Unauthorized(new { error = "Invalid signature" });
                }
            }

            // Deserialize the activity - use IObjectOrLink to preserve concrete types
            IObjectOrLink? activity;
            try
            {
                activity = JsonSerializer.Deserialize<IObjectOrLink>(body, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON in shared inbox request");
                return BadRequest(new { error = "Invalid JSON" });
            }

            if (activity == null)
            {
                return BadRequest(new { error = "Failed to parse activity" });
            }

            var activityObj = activity as IObject;
            var activityId = activityObj?.Id;
            var activityType = activityObj?.Type?.FirstOrDefault();
            var activityActor = activityObj is Activity act ? act.Actor?.FirstOrDefault() : null;
            
            _logger.LogInformation("Shared inbox received activity: Type={Type}, Id={Id}, Actor={Actor}, ConcreteType={ConcreteType}",
                activityType,
                activityId,
                activityActor is ILink actorLink ? actorLink.Href?.ToString() : "unknown",
                activity.GetType().Name);

            if (activity is Activity actForAddressing)
            {
                _logger.LogDebug("Activity addressing - To: {To}, Cc: {Cc}, Bcc: {Bcc}",
                    string.Join(", ", actForAddressing.To?.Select(t => t is ILink l ? l.Href?.ToString() : "?") ?? Array.Empty<string>()),
                    string.Join(", ", actForAddressing.Cc?.Select(t => t is ILink l ? l.Href?.ToString() : "?") ?? Array.Empty<string>()),
                    string.Join(", ", actForAddressing.Bcc?.Select(t => t is ILink l ? l.Href?.ToString() : "?") ?? Array.Empty<string>()));
            }

            // Determine local recipients from addressing
            if (activity is not Activity activityWithAddressing)
            {
                _logger.LogWarning("Activity is not an Activity type, cannot determine recipients (Type={Type})",
                    activity.GetType().Name);
                return Accepted(); // Not an error, just not processable for delivery
            }
            
            var localRecipients = await GetLocalRecipientsAsync(activityWithAddressing);
            
            if (!localRecipients.Any())
            {
                _logger.LogWarning("No local recipients found for shared inbox activity (Type={Type}, Id={Id})",
                    activity.Type?.FirstOrDefault(), activity.Id);
                return Accepted(); // Accept but don't process
            }

            _logger.LogInformation("Delivering to {Count} local recipients: {Recipients}",
                localRecipients.Count, string.Join(", ", localRecipients));

            // Process for each local recipient
            var tasks = localRecipients.Select(async username =>
            {
                try
                {
                    _logger.LogDebug("Processing activity for recipient: {Username}", username);
                    await _inboxHandler.HandleActivityAsync(username, activity, false, CancellationToken.None);
                    _logger.LogInformation("Successfully delivered to {Username}", username);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error delivering to {Username}", username);
                }
            });

            await Task.WhenAll(tasks);
            _logger.LogInformation("Shared inbox processing complete for {Count} recipients", localRecipients.Count);
            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing shared inbox POST");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private async Task<bool> VerifySignatureAsync(string body, CancellationToken cancellationToken)
    {
        try
        {
            if (!Request.Headers.TryGetValue("Signature", out var signatureHeader))
            {
                return false;
            }

            // Parse signature to get keyId
            var keyId = ExtractKeyId(signatureHeader.ToString());
            if (string.IsNullOrEmpty(keyId))
            {
                return false;
            }

            ValidateRequestClockSkew(Request);

            // Validate Digest header for POST requests
            if (Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                if (!Request.Headers.TryGetValue("Digest", out var digestHeader))
                {
                    _logger.LogWarning("Shared inbox POST request missing Digest header");
                }
                else
                {
                    var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);
                    var expectedDigest = _signatureService.ComputeContentDigestHash(bodyBytes);
                    var digestValue = digestHeader.ToString();
                    
                    if (digestValue.StartsWith("SHA-256=", StringComparison.OrdinalIgnoreCase))
                    {
                        var providedDigest = digestValue.Substring(8);
                        if (providedDigest != expectedDigest)
                        {
                            _logger.LogWarning("Shared inbox Digest mismatch. Expected: {Expected}, Got: {Got}", 
                                expectedDigest, providedDigest);
                            return false;
                        }
                        _logger.LogDebug("Shared inbox Digest header validated successfully");
                    }
                }
            }

            // Fetch public key
            var publicKeyPem = await FetchPublicKeyAsync(keyId, cancellationToken);
            if (string.IsNullOrEmpty(publicKeyPem))
            {
                return false;
            }

            // Verify signature
            var headers = new Dictionary<string, string>();
            
            // Add (request-target) pseudo-header
            var requestTarget = $"{Request.Method.ToLower()} {Request.Path}";
            headers["(request-target)"] = requestTarget;
            
            // Add all headers from the request (lowercase keys)
            foreach (var header in Request.Headers)
            {
                headers[header.Key.ToLowerInvariant()] = header.Value.ToString();
            }
            
            return await _signatureService.VerifyHttpSignatureAsync(
                headers,
                publicKeyPem,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying signature");
            return false;
        }
    }

    private string? ExtractKeyId(string signatureHeader)
    {
        foreach (var param in signatureHeader.Split(','))
        {
            var parts = param.Trim().Split('=', 2);
            if (parts.Length == 2 && parts[0] == "keyId")
            {
                return parts[1].Trim('"');
            }
        }
        return null;
    }

    private async Task<string?> FetchPublicKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        var cacheKey = $"publickey:{keyId}";
        if (_cache.TryGetValue<string>(cacheKey, out var cachedKey))
        {
            return cachedKey;
        }

        try
        {
            var actorUrl = keyId.Split('#')[0];
            Actor? actor = await _actorRepository.GetActorByIdAsync(actorUrl, cancellationToken);

            if (actor == null)
            {
                actor = await _activityPubClient.GetActorAsync(new Uri(actorUrl), cancellationToken);
            }

            if (actor?.ExtensionData != null &&
                actor.ExtensionData.TryGetValue("publicKey", out var publicKeyElement) &&
                publicKeyElement is JsonElement publicKey &&
                publicKey.TryGetProperty("publicKeyPem", out var pemElement))
            {
                var pem = pemElement.GetString();
                _cache.Set(cacheKey, pem, TimeSpan.FromHours(1));
                return pem;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching public key for {KeyId}", keyId);
        }

        return null;
    }

    private async Task<List<string>> GetLocalRecipientsAsync(Activity activity)
    {
        var recipients = new HashSet<string>();
        const string PublicAddress = "https://www.w3.org/ns/activitystreams#Public";
        var hasPublicAddressing = false;

        async Task AddRecipientsAsync(IEnumerable<IObjectOrLink>? items, string fieldName)
        {
            if (items == null) return;
            
            foreach (var item in items)
            {
                if (item is ILink link && link.Href != null)
                {
                    var url = link.Href.ToString();
                    _logger.LogDebug("Processing {Field} recipient: {Url}", fieldName, url);
                    
                    if (url == PublicAddress)
                    {
                        _logger.LogDebug("Found public addressing in {Field}", fieldName);
                        hasPublicAddressing = true;
                        continue;
                    }
                    
                    if (url.EndsWith("/followers"))
                    {
                        var actorId = url.Substring(0, url.Length - "/followers".Length);
                        _logger.LogDebug("Processing followers collection for actor: {ActorId}", actorId);
                        var actor = await _actorRepository.GetActorByIdAsync(actorId);
                        
                        if (actor?.PreferredUsername != null)
                        {
                            var followers = await _actorRepository.GetFollowersAsync(actor.PreferredUsername);
                            _logger.LogDebug("Found {Count} followers for {Username}", followers.Count(), actor.PreferredUsername);
                            foreach (var followerUrl in followers)
                            {
                                var followerActor = await _actorRepository.GetActorByIdAsync(followerUrl);
                                if (followerActor?.PreferredUsername != null)
                                {
                                    _logger.LogDebug("Adding follower to recipients: {Username}", followerActor.PreferredUsername);
                                    recipients.Add(followerActor.PreferredUsername);
                                }
                                else
                                {
                                    _logger.LogDebug("Follower not found locally: {FollowerUrl}", followerUrl);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogDebug("Actor not found locally for followers collection: {ActorId}, checking local users' following lists", actorId);
                            var allLocalUsers = await _actorRepository.GetAllLocalUsernamesAsync();
                            foreach (var username in allLocalUsers)
                            {
                                var following = await _actorRepository.GetFollowingAsync(username);
                                if (following.Contains(actorId))
                                {
                                    _logger.LogDebug("Local user {Username} follows {ActorId}, adding as recipient", username, actorId);
                                    recipients.Add(username);
                                }
                            }
                        }
                    }
                    else
                    {
                        var actor = await _actorRepository.GetActorByIdAsync(url);
                        if (actor?.PreferredUsername != null)
                        {
                            _logger.LogDebug("Adding direct recipient: {Username}", actor.PreferredUsername);
                            recipients.Add(actor.PreferredUsername);
                        }
                        else
                        {
                            _logger.LogDebug("Actor not found locally: {Url}", url);
                        }
                    }
                }
            }
        }

        await AddRecipientsAsync(activity.To, "To");
        await AddRecipientsAsync(activity.Cc, "Cc");
        await AddRecipientsAsync(activity.Bcc, "Bcc");

        if (hasPublicAddressing)
        {
            _logger.LogDebug("Fetching all local usernames for public addressing");
            var allLocalUsers = await _actorRepository.GetAllLocalUsernamesAsync();
            _logger.LogInformation("GetAllLocalUsernamesAsync returned {Count} users: [{Users}]. Repository instance: {RepositoryId}", 
                allLocalUsers.Count(), string.Join(", ", allLocalUsers), _actorRepository.GetHashCode());
            foreach (var username in allLocalUsers)
            {
                recipients.Add(username);
            }
        }

        _logger.LogDebug("Total unique recipients resolved: {Count}", recipients.Count);
        return recipients.ToList();
    }
}
