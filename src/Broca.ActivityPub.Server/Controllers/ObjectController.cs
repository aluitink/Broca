using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;

namespace Broca.ActivityPub.Server.Controllers;

/// <summary>
/// Controller for serving individual ActivityPub objects and activities
/// Each object needs a unique, dereferenceable URI as per ActivityPub spec
/// </summary>
[ApiController]
[Route("users/{username}/objects")]
public class ObjectController : ActivityPubControllerBase
{
    private readonly IActivityRepository _activityRepository;
    private readonly IActorRepository _actorRepository;
    private readonly ObjectEnrichmentService _enrichmentService;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<ObjectController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ObjectController(
        IActivityRepository activityRepository,
        IActorRepository actorRepository,
        ObjectEnrichmentService enrichmentService,
        IOptions<ActivityPubServerOptions> options,
        ILogger<ObjectController> logger)
    {
        _activityRepository = activityRepository;
        _actorRepository = actorRepository;
        _enrichmentService = enrichmentService;
        _options = options.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Get an individual object/activity by ID
    /// This endpoint provides unique URIs for all objects (notes, articles, etc.)
    /// </summary>
    [HttpGet("{objectId}")]
    [Produces("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> GetObject(string username, string objectId)
    {
        try
        {
            // Verify actor exists
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            // Construct the full object ID from the route
            var baseUrl = GetBaseUrl(_options.NormalizedRoutePrefix);
            var fullObjectId = $"{baseUrl}/users/{username}/objects/{objectId}";

            _logger.LogDebug("Looking for object with full ID: {FullObjectId}", fullObjectId);

            // Get the object/activity
            var obj = await _activityRepository.GetActivityByIdAsync(fullObjectId);
            if (obj == null)
            {
                _logger.LogWarning("Object not found with ID: {FullObjectId}", fullObjectId);
                return NotFound(new { error = "Object not found" });
            }

            // Check if the object is a Tombstone (deleted)
            if (obj is Tombstone)
            {
                _logger.LogInformation("Object {FullObjectId} has been deleted", fullObjectId);
                return StatusCode(410, new { error = "Object has been deleted" });
            }

            // Ensure the object has the correct ID set
            if (obj is KristofferStrube.ActivityStreams.Object asObject && string.IsNullOrEmpty(asObject.Id))
            {
                asObject.Id = fullObjectId;
            }

            // Enrich the object with collection metadata (replies, likes, shares)
            await _enrichmentService.EnrichActivityAsync(obj, baseUrl);

            return Ok(obj);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving object {ObjectId} for {Username}", objectId, username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get replies to an object
    /// Returns an OrderedCollection of reply activities
    /// </summary>
    [HttpGet("{objectId}/replies")]
    [Produces("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> GetReplies(string username, string objectId, [FromQuery] int page = 0, [FromQuery] int limit = 20)
    {
        try
        {
            // Verify actor exists
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            // Construct the full object ID
            var baseUrl = GetBaseUrl(_options.NormalizedRoutePrefix);
            var fullObjectId = $"{baseUrl}/users/{username}/objects/{objectId}";

            // Verify object exists
            var obj = await _activityRepository.GetActivityByIdAsync(fullObjectId);
            if (obj == null)
            {
                return NotFound(new { error = "Object not found" });
            }

            var search = GetSearchParameters();
            var offset = page * limit;

            IEnumerable<IObjectOrLink> replies;
            int totalCount;
            bool itemsAlreadyPaginated;

            if (search?.HasSearchCriteria == true && _activityRepository is ISearchableActivityRepository searchableRepo)
            {
                replies = await searchableRepo.GetRepliesAsync(fullObjectId, search, limit, offset);
                totalCount = await searchableRepo.GetRepliesCountAsync(fullObjectId, search);
                itemsAlreadyPaginated = true;
            }
            else if (search?.HasSearchCriteria == true)
            {
                replies = await _activityRepository.GetRepliesAsync(fullObjectId, int.MaxValue, 0);
                totalCount = replies.Count();
                itemsAlreadyPaginated = false;
            }
            else
            {
                replies = await _activityRepository.GetRepliesAsync(fullObjectId, limit, offset);
                totalCount = await _activityRepository.GetRepliesCountAsync(fullObjectId);
                itemsAlreadyPaginated = true;
            }

            var collectionUrl = $"{baseUrl}/users/{username}/objects/{objectId}/replies";
            return BuildCollectionResponse(collectionUrl, replies, totalCount, page, limit, search, itemsAlreadyPaginated);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { error = $"Invalid search parameter: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving replies for object {ObjectId}", objectId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get likes for an object
    /// Returns an OrderedCollection of Like activities
    /// </summary>
    [HttpGet("{objectId}/likes")]
    [Produces("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> GetLikes(string username, string objectId, [FromQuery] int page = 0, [FromQuery] int limit = 20)
    {
        try
        {
            // Verify actor exists
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            // Construct the full object ID
            var baseUrl = GetBaseUrl(_options.NormalizedRoutePrefix);
            var fullObjectId = $"{baseUrl}/users/{username}/objects/{objectId}";

            // Verify object exists
            var obj = await _activityRepository.GetActivityByIdAsync(fullObjectId);
            if (obj == null)
            {
                return NotFound(new { error = "Object not found" });
            }

            var search = GetSearchParameters();
            var offset = page * limit;

            IEnumerable<IObjectOrLink> likes;
            int totalCount;
            bool itemsAlreadyPaginated;

            if (search?.HasSearchCriteria == true)
            {
                likes = await _activityRepository.GetLikesAsync(fullObjectId, int.MaxValue, 0);
                totalCount = likes.Count();
                itemsAlreadyPaginated = false;
            }
            else
            {
                likes = await _activityRepository.GetLikesAsync(fullObjectId, limit, offset);
                totalCount = await _activityRepository.GetLikesCountAsync(fullObjectId);
                itemsAlreadyPaginated = true;
            }

            var collectionUrl = $"{baseUrl}/users/{username}/objects/{objectId}/likes";
            return BuildCollectionResponse(collectionUrl, likes, totalCount, page, limit, search, itemsAlreadyPaginated);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { error = $"Invalid search parameter: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving likes for object {ObjectId}", objectId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get shares/announces for an object
    /// Returns an OrderedCollection of Announce activities
    /// </summary>
    [HttpGet("{objectId}/shares")]
    [Produces("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> GetShares(string username, string objectId, [FromQuery] int page = 0, [FromQuery] int limit = 20)
    {
        try
        {
            // Verify actor exists
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            // Construct the full object ID
            var baseUrl = GetBaseUrl(_options.NormalizedRoutePrefix);
            var fullObjectId = $"{baseUrl}/users/{username}/objects/{objectId}";

            // Verify object exists
            var obj = await _activityRepository.GetActivityByIdAsync(fullObjectId);
            if (obj == null)
            {
                return NotFound(new { error = "Object not found" });
            }

            var search = GetSearchParameters();
            var offset = page * limit;

            IEnumerable<IObjectOrLink> shares;
            int totalCount;
            bool itemsAlreadyPaginated;

            if (search?.HasSearchCriteria == true)
            {
                shares = await _activityRepository.GetSharesAsync(fullObjectId, int.MaxValue, 0);
                totalCount = shares.Count();
                itemsAlreadyPaginated = false;
            }
            else
            {
                shares = await _activityRepository.GetSharesAsync(fullObjectId, limit, offset);
                totalCount = await _activityRepository.GetSharesCountAsync(fullObjectId);
                itemsAlreadyPaginated = true;
            }

            var collectionUrl = $"{baseUrl}/users/{username}/objects/{objectId}/shares";
            return BuildCollectionResponse(collectionUrl, shares, totalCount, page, limit, search, itemsAlreadyPaginated);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { error = $"Invalid search parameter: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shares for object {ObjectId}", objectId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
