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

namespace Broca.ActivityPub.Server.Controllers;

[ApiController]
[Route("users/{username}/outbox")]
public class OutboxController : ActivityPubControllerBase
{
    private readonly IActivityRepository _activityRepository;
    private readonly IActorRepository _actorRepository;
    private readonly OutboxProcessor _outboxProcessor;
    private readonly AttachmentProcessingService _attachmentProcessingService;
    private readonly ObjectEnrichmentService _enrichmentService;
    private readonly HttpSignatureService _signatureService;
    private readonly IMemoryCache _cache;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<OutboxController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private static readonly TimeSpan PublicKeyCacheDuration = TimeSpan.FromHours(1);

    public OutboxController(
        IActivityRepository activityRepository,
        IActorRepository actorRepository,
        OutboxProcessor outboxProcessor,
        AttachmentProcessingService attachmentProcessingService,
        ObjectEnrichmentService enrichmentService,
        HttpSignatureService signatureService,
        IMemoryCache cache,
        IOptions<ActivityPubServerOptions> options,
        ILogger<OutboxController> logger)
    {
        _activityRepository = activityRepository;
        _actorRepository = actorRepository;
        _outboxProcessor = outboxProcessor;
        _attachmentProcessingService = attachmentProcessingService;
        _enrichmentService = enrichmentService;
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
            var activities = await _activityRepository.GetOutboxActivitiesAsync(username, limit, offset);
            
            var baseUrl = GetBaseUrl(_options.NormalizedRoutePrefix);
            
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
            
            var totalCount = await _activityRepository.GetOutboxCountAsync(username);

            // Check if pagination parameters were explicitly provided
            var hasPageParam = Request.Query.ContainsKey("page");
            var hasLimitParam = Request.Query.ContainsKey("limit");

            if (!hasPageParam && !hasLimitParam)
            {
                // Return the collection wrapper when no pagination params provided
                var collection = new OrderedCollection
                {
                    JsonLDContext = new List<ITermDefinition> 
                    { 
                        new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) 
                    },
                    Id = $"{baseUrl}/users/{username}/outbox",
                    TotalItems = (uint)totalCount,
                    First = totalCount > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/outbox?page=0&limit={limit}") } 
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
                    Id = $"{baseUrl}/users/{username}/outbox?page={page}&limit={limit}",
                    PartOf = new Link { Href = new Uri($"{baseUrl}/users/{username}/outbox") },
                    TotalItems = (uint)totalCount,
                    OrderedItems = activities.ToList(),
                    Next = (offset + limit < totalCount) 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/outbox?page={page + 1}&limit={limit}") }
                        : null,
                    Prev = page > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/outbox?page={page - 1}&limit={limit}") }
                        : null
                };
                return Ok(collectionPage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving outbox for {Username}", username);
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
                _logger.LogWarning(ex, "Invalid JSON in outbox request for {Username}", username);
                return BadRequest(new { error = "Invalid JSON" });
            }

            if (activity == null)
            {
                return BadRequest(new { error = "Failed to parse activity" });
            }

            if (_options.RequireHttpSignatures)
            {
                try
                {
                    var authError = await VerifyCallerSignatureAsync(body, actor, HttpContext.RequestAborted);
                    if (authError != null)
                        return authError;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Signature verification failed for outbox POST by {Username}", username);
                    return Unauthorized(new { error = "Signature verification failed", detail = ex.Message });
                }
            }

            // Process the activity
            var activityId = await _outboxProcessor.ProcessActivityAsync(username, activity, HttpContext.RequestAborted);

            return Created(activityId, new { id = activityId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing outbox POST for {Username}", username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private async Task<IActionResult?> VerifyCallerSignatureAsync(string body, Actor localActor, CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("Signature", out var signatureHeader) || string.IsNullOrEmpty(signatureHeader))
            return Unauthorized(new { error = "Signature header is missing" });

        var keyId = _signatureService.GetSignatureKeyId(signatureHeader!);

        ValidateRequestClockSkew(Request);

        var publicKeyPem = await FetchLocalActorPublicKeyAsync(keyId, cancellationToken);
        if (string.IsNullOrWhiteSpace(publicKeyPem))
            return Unauthorized(new { error = $"Could not retrieve public key for keyId: {keyId}" });

        var headers = new Dictionary<string, string>
        {
            ["signature"] = signatureHeader!,
            ["(request-target)"] = $"{Request.Method.ToLower()} {Request.Path}"
        };

        foreach (var header in Request.Headers)
        {
            var name = header.Key.ToLower();
            if (name != "signature")
                headers[name] = header.Value.ToString();
        }

        if (Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && Request.Headers.TryGetValue("Digest", out var digestHeader))
        {
            var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);
            var expectedDigest = _signatureService.ComputeContentDigestHash(bodyBytes);
            var digestValue = digestHeader.ToString();
            if (digestValue.StartsWith("SHA-256=") && digestValue.Substring(8) != expectedDigest)
                throw new InvalidOperationException("Digest header does not match request body");
        }

        var isValid = await _signatureService.VerifyHttpSignatureAsync(headers, publicKeyPem, cancellationToken);
        if (!isValid)
            return Unauthorized(new { error = "Invalid signature" });

        var callerActorUrl = keyId.Split('#')[0];
        if (!string.Equals(callerActorUrl, localActor.Id, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Outbox POST rejected: signature keyId actor {CallerActor} does not match outbox owner {OwnerActor}",
                callerActorUrl, localActor.Id);
            return StatusCode(403, new { error = "Forbidden: signature does not belong to this actor" });
        }

        return null;
    }

    private async Task<string?> FetchLocalActorPublicKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        var cacheKey = $"outbox-publickey:{keyId}";
        if (_cache.TryGetValue<string>(cacheKey, out var cached))
            return cached;

        var actorUrl = keyId.Split('#')[0];
        var actor = await _actorRepository.GetActorByIdAsync(actorUrl, cancellationToken);

        if (actor?.ExtensionData == null || !actor.ExtensionData.TryGetValue("publicKey", out var publicKeyObj))
            return null;

        string? pem = null;
        if (publicKeyObj is JsonElement je && je.TryGetProperty("publicKeyPem", out var pemEl))
            pem = pemEl.GetString();

        if (!string.IsNullOrWhiteSpace(pem))
            _cache.Set(cacheKey, pem, PublicKeyCacheDuration);

        return pem;
    }
}
