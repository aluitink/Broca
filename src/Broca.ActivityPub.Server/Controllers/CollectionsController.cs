using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Server.Services;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Broca.ActivityPub.Server.Controllers;

/// <summary>
/// Controller for managing and accessing custom collections
/// </summary>
[ApiController]
[Route("users/{username}/collections")]
public class CollectionsController : ActivityPubControllerBase
{
    private readonly ICollectionService _collectionService;
    private readonly IActorRepository _actorRepository;
    private readonly ObjectEnrichmentService _enrichmentService;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<CollectionsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public CollectionsController(
        ICollectionService collectionService,
        IActorRepository actorRepository,
        ObjectEnrichmentService enrichmentService,
        IOptions<ActivityPubServerOptions> options,
        ILogger<CollectionsController> logger)
    {
        _collectionService = collectionService;
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
    /// Gets all collections for a user (collections catalog)
    /// </summary>
    [HttpGet]
    [Produces("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> GetCollections(string username)
    {
        try
        {
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            var definitions = await _collectionService.GetCollectionDefinitionsAsync(username);
            var baseUrl = GetBaseUrl(_options.NormalizedRoutePrefix);

            // Filter by visibility - only show public collections unless authenticated
            // TODO: Add authentication check for private collections
            var visibleDefinitions = definitions.Where(d => d.Visibility == CollectionVisibility.Public).ToList();

            var collectionItems = visibleDefinitions.Select(d => new Collection
            {
                Id = $"{baseUrl}/users/{username}/collections/{d.Id}",
                Type = new List<string> { "Collection" },
                Name = new List<string> { d.Name },
                Summary = d.Description != null ? new List<string> { d.Description } : null
            } as IObjectOrLink).ToList();

            var catalog = new OrderedCollection
            {
                JsonLDContext = new List<ITermDefinition>
                {
                    new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
                },
                Id = $"{baseUrl}/users/{username}/collections",
                Type = new List<string> { "OrderedCollection" },
                TotalItems = (uint)collectionItems.Count,
                OrderedItems = collectionItems
            };

            return Ok(catalog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving collections for {Username}", username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Gets a specific collection
    /// </summary>
    [HttpGet("{collectionId}")]
    [Produces("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> GetCollection(
        string username, 
        string collectionId,
        [FromQuery] int page = 0,
        [FromQuery] int limit = 20)
    {
        try
        {
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            var definition = await _collectionService.GetCollectionDefinitionAsync(username, collectionId);
            if (definition == null)
            {
                return NotFound(new { error = "Collection not found" });
            }

            // Check visibility
            // TODO: Add authentication check for private collections
            if (definition.Visibility == CollectionVisibility.Private)
            {
                return StatusCode(403, new { error = "Collection is private" });
            }

            var offset = page * limit;
            var items = await _collectionService.GetCollectionItemsAsync(username, collectionId, limit, offset);
            var totalCount = await _collectionService.GetCollectionItemCountAsync(username, collectionId);
            var baseUrl = GetBaseUrl(_options.NormalizedRoutePrefix);

            // Enrich items with collection information
            await _enrichmentService.EnrichActivitiesAsync(items, baseUrl);

            var collection = new OrderedCollectionPage
            {
                JsonLDContext = new List<ITermDefinition>
                {
                    new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
                },
                Id = $"{baseUrl}/users/{username}/collections/{collectionId}?page={page}&limit={limit}",
                Type = new List<string> { "OrderedCollectionPage" },
                PartOf = new Link { Href = new Uri($"{baseUrl}/users/{username}/collections/{collectionId}") },
                TotalItems = (uint)totalCount,
                OrderedItems = items.ToList(),
                Next = (offset + limit < totalCount)
                    ? new Link { Href = new Uri($"{baseUrl}/users/{username}/collections/{collectionId}?page={page + 1}&limit={limit}") }
                    : null,
                Prev = page > 0
                    ? new Link { Href = new Uri($"{baseUrl}/users/{username}/collections/{collectionId}?page={page - 1}&limit={limit}") }
                    : null
            };

            return Ok(collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving collection {CollectionId} for {Username}", collectionId, username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Gets the definition of a collection
    /// </summary>
    [HttpGet("{collectionId}/definition")]
    [Produces("application/json")]
    public async Task<IActionResult> GetCollectionDefinition(string username, string collectionId)
    {
        try
        {
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            var definition = await _collectionService.GetCollectionDefinitionAsync(username, collectionId);
            if (definition == null)
            {
                return NotFound(new { error = "Collection not found" });
            }

            // Check visibility
            // TODO: Add authentication check for private collections
            if (definition.Visibility == CollectionVisibility.Private)
            {
                return StatusCode(403, new { error = "Collection is private" });
            }

            return Ok(definition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving collection definition {CollectionId} for {Username}", collectionId, username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Adds an item to a manual collection
    /// </summary>
    [HttpPost("{collectionId}/items")]
    [Consumes("application/json")]
    public async Task<IActionResult> AddItem(string username, string collectionId, [FromBody] AddItemRequest request)
    {
        try
        {
            // TODO: Add authentication and authorization

            await _collectionService.AddItemToCollectionAsync(username, collectionId, request.ItemId);
            return Ok(new { message = "Item added to collection" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to collection {CollectionId} for {Username}", collectionId, username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Removes an item from a manual collection
    /// </summary>
    [HttpDelete("{collectionId}/items/{itemId}")]
    public async Task<IActionResult> RemoveItem(string username, string collectionId, string itemId)
    {
        try
        {
            // TODO: Add authentication and authorization

            await _collectionService.RemoveItemFromCollectionAsync(username, collectionId, itemId);
            return Ok(new { message = "Item removed from collection" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item from collection {CollectionId} for {Username}", collectionId, username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public class AddItemRequest
{
    public string ItemId { get; set; } = string.Empty;
}
