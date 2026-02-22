using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Server.Services;
using Microsoft.AspNetCore.Mvc;
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
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<OutboxController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public OutboxController(
        IActivityRepository activityRepository,
        IActorRepository actorRepository,
        OutboxProcessor outboxProcessor,
        AttachmentProcessingService attachmentProcessingService,
        ObjectEnrichmentService enrichmentService,
        IOptions<ActivityPubServerOptions> options,
        ILogger<OutboxController> logger)
    {
        _activityRepository = activityRepository;
        _actorRepository = actorRepository;
        _outboxProcessor = outboxProcessor;
        _attachmentProcessingService = attachmentProcessingService;
        _enrichmentService = enrichmentService;
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
}
