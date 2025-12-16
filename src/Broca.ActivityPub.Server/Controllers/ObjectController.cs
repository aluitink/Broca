using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
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
public class ObjectController : ControllerBase
{
    private readonly IActivityRepository _activityRepository;
    private readonly IActorRepository _actorRepository;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<ObjectController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ObjectController(
        IActivityRepository activityRepository,
        IActorRepository actorRepository,
        IOptions<ActivityPubServerOptions> options,
        ILogger<ObjectController> logger)
    {
        _activityRepository = activityRepository;
        _actorRepository = actorRepository;
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

            // Get the object/activity
            var obj = await _activityRepository.GetActivityByIdAsync(objectId);
            if (obj == null)
            {
                return NotFound(new { error = "Object not found" });
            }

            // Ensure the object has the correct ID set
            var baseUrl = $"{Request.Scheme}://{Request.Host}{_options.NormalizedRoutePrefix}";
            if (obj is KristofferStrube.ActivityStreams.Object asObject && string.IsNullOrEmpty(asObject.Id))
            {
                asObject.Id = $"{baseUrl}/users/{username}/objects/{objectId}";
            }

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

            // Verify object exists
            var obj = await _activityRepository.GetActivityByIdAsync(objectId);
            if (obj == null)
            {
                return NotFound(new { error = "Object not found" });
            }

            var offset = page * limit;
            var replies = await _activityRepository.GetRepliesAsync(objectId, limit, offset);
            var totalCount = await _activityRepository.GetRepliesCountAsync(objectId);
            var baseUrl = $"{Request.Scheme}://{Request.Host}{_options.NormalizedRoutePrefix}";

            if (page == 0 && limit == 20)
            {
                // Return the collection wrapper
                var collection = new OrderedCollection
                {
                    JsonLDContext = new List<ITermDefinition> 
                    { 
                        new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) 
                    },
                    Id = $"{baseUrl}/users/{username}/objects/{objectId}/replies",
                    TotalItems = (uint)totalCount,
                    First = totalCount > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/objects/{objectId}/replies?page=0&limit={limit}") } 
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
                    Id = $"{baseUrl}/users/{username}/objects/{objectId}/replies?page={page}&limit={limit}",
                    PartOf = new Link { Href = new Uri($"{baseUrl}/users/{username}/objects/{objectId}/replies") },
                    TotalItems = (uint)totalCount,
                    OrderedItems = replies.ToList(),
                    Next = (offset + limit < totalCount) 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/objects/{objectId}/replies?page={page + 1}&limit={limit}") }
                        : null,
                    Prev = page > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/objects/{objectId}/replies?page={page - 1}&limit={limit}") }
                        : null
                };
                return Ok(collectionPage);
            }
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

            // Verify object exists
            var obj = await _activityRepository.GetActivityByIdAsync(objectId);
            if (obj == null)
            {
                return NotFound(new { error = "Object not found" });
            }

            var offset = page * limit;
            var likes = await _activityRepository.GetLikesAsync(objectId, limit, offset);
            var totalCount = await _activityRepository.GetLikesCountAsync(objectId);
            var baseUrl = $"{Request.Scheme}://{Request.Host}{_options.NormalizedRoutePrefix}";

            if (page == 0 && limit == 20)
            {
                // Return the collection wrapper
                var collection = new OrderedCollection
                {
                    JsonLDContext = new List<ITermDefinition> 
                    { 
                        new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) 
                    },
                    Id = $"{baseUrl}/users/{username}/objects/{objectId}/likes",
                    TotalItems = (uint)totalCount,
                    First = totalCount > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/objects/{objectId}/likes?page=0&limit={limit}") } 
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
                    Id = $"{baseUrl}/users/{username}/objects/{objectId}/likes?page={page}&limit={limit}",
                    PartOf = new Link { Href = new Uri($"{baseUrl}/users/{username}/objects/{objectId}/likes") },
                    TotalItems = (uint)totalCount,
                    OrderedItems = likes.ToList(),
                    Next = (offset + limit < totalCount) 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/objects/{objectId}/likes?page={page + 1}&limit={limit}") }
                        : null,
                    Prev = page > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/objects/{objectId}/likes?page={page - 1}&limit={limit}") }
                        : null
                };
                return Ok(collectionPage);
            }
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

            // Verify object exists
            var obj = await _activityRepository.GetActivityByIdAsync(objectId);
            if (obj == null)
            {
                return NotFound(new { error = "Object not found" });
            }

            var offset = page * limit;
            var shares = await _activityRepository.GetSharesAsync(objectId, limit, offset);
            var totalCount = await _activityRepository.GetSharesCountAsync(objectId);
            var baseUrl = $"{Request.Scheme}://{Request.Host}{_options.NormalizedRoutePrefix}";

            if (page == 0 && limit == 20)
            {
                // Return the collection wrapper
                var collection = new OrderedCollection
                {
                    JsonLDContext = new List<ITermDefinition> 
                    { 
                        new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) 
                    },
                    Id = $"{baseUrl}/users/{username}/objects/{objectId}/shares",
                    TotalItems = (uint)totalCount,
                    First = totalCount > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/objects/{objectId}/shares?page=0&limit={limit}") } 
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
                    Id = $"{baseUrl}/users/{username}/objects/{objectId}/shares?page={page}&limit={limit}",
                    PartOf = new Link { Href = new Uri($"{baseUrl}/users/{username}/objects/{objectId}/shares") },
                    TotalItems = (uint)totalCount,
                    OrderedItems = shares.ToList(),
                    Next = (offset + limit < totalCount) 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/objects/{objectId}/shares?page={page + 1}&limit={limit}") }
                        : null,
                    Prev = page > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/objects/{objectId}/shares?page={page - 1}&limit={limit}") }
                        : null
                };
                return Ok(collectionPage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shares for object {ObjectId}", objectId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
