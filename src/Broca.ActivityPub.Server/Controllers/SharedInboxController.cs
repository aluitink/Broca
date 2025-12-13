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
public class SharedInboxController : ControllerBase
{
    private readonly IInboxHandler _inboxHandler;
    private readonly IActorRepository _actorRepository;
    private readonly HttpSignatureService _signatureService;
    private readonly IMemoryCache _cache;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<SharedInboxController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SharedInboxController(
        IInboxHandler inboxHandler,
        IActorRepository actorRepository,
        HttpSignatureService signatureService,
        IMemoryCache cache,
        IOptions<ActivityPubServerOptions> options,
        ILogger<SharedInboxController> logger)
    {
        _inboxHandler = inboxHandler;
        _actorRepository = actorRepository;
        _signatureService = signatureService;
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

            // Deserialize the activity
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

            _logger.LogInformation("Shared inbox received activity");

            // Determine local recipients from addressing
            var localRecipients = activity is Activity act ? 
                await GetLocalRecipientsAsync(act) : 
                new List<string>();
            
            if (!localRecipients.Any())
            {
                _logger.LogWarning("No local recipients found for shared inbox activity");
                return Accepted(); // Accept but don't process
            }

            _logger.LogInformation("Delivering to {Count} local recipients", localRecipients.Count);

            // Process for each local recipient
            var tasks = localRecipients.Select(async username =>
            {
                try
                {
                    await _inboxHandler.HandleActivityAsync(username, activity, CancellationToken.None);
                    _logger.LogDebug("Delivered to {Username}", username);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error delivering to {Username}", username);
                }
            });

            await Task.WhenAll(tasks);
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

            // Fetch public key
            var publicKeyPem = await FetchPublicKeyAsync(keyId, cancellationToken);
            if (string.IsNullOrEmpty(publicKeyPem))
            {
                return false;
            }

            // Verify signature
            var headers = Request.Headers.ToDictionary(
                h => h.Key.ToLowerInvariant(),
                h => h.Value.ToString()
            );
            
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
            var actorUrl = keyId.Contains('#') ? keyId.Split('#')[0] : keyId;
            var actor = await _actorRepository.GetActorByIdAsync(actorUrl, cancellationToken);
            
            if (actor?.ExtensionData != null &&
                actor.ExtensionData.TryGetValue("publicKey", out var publicKeyElement))
            {
                var publicKey = JsonSerializer.Deserialize<JsonElement>(publicKeyElement);
                if (publicKey.TryGetProperty("publicKeyPem", out var pemElement))
                {
                    var pem = pemElement.GetString();
                    _cache.Set(cacheKey, pem, TimeSpan.FromHours(1));
                    return pem;
                }
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
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        void AddRecipients(IEnumerable<IObjectOrLink>? items)
        {
            if (items == null) return;
            
            foreach (var item in items)
            {
                if (item is Link { Href: Uri href })
                {
                    var url = href.ToString();
                    if (url.StartsWith(baseUrl))
                    {
                        var parts = url.Split('/');
                        if (parts.Length >= 2 && parts[^2] == "users")
                        {
                            recipients.Add(parts[^1]);
                        }
                    }
                }
            }
        }

        AddRecipients(activity.To);
        AddRecipients(activity.Cc);
        AddRecipients(activity.Bcc);

        // Verify users exist
        var validRecipients = new List<string>();
        foreach (var username in recipients)
        {
            if (await _actorRepository.GetActorByUsernameAsync(username) != null)
            {
                validRecipients.Add(username);
            }
        }

        return validRecipients;
    }
}
