using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;

namespace Broca.ActivityPub.Server.Controllers;

[ApiController]
[Route("users/{username}")]
public class ActorController : ControllerBase
{
    private readonly IActorRepository _actorRepository;
    private readonly IActivityRepository _activityRepository;
    private readonly ICollectionService _collectionService;
    private readonly IdentityProviderService? _identityProviderService;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<ActorController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ActorController(
        IActorRepository actorRepository,
        IActivityRepository activityRepository,
        ICollectionService collectionService,
        IOptions<ActivityPubServerOptions> options,
        ILogger<ActorController> logger,
        IdentityProviderService? identityProviderService = null)
    {
        _actorRepository = actorRepository;
        _activityRepository = activityRepository;
        _collectionService = collectionService;
        _identityProviderService = identityProviderService;
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
    public async Task<IActionResult> Get(string username)
    {
        try
        {
            // Try to get from repository first
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            
            // If not found and identity provider is configured, try to create from provider
            if (actor == null && _identityProviderService != null)
            {
                actor = await _identityProviderService.GetOrCreateActorAsync(username);
            }
            
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            // Clone the actor to avoid modifying the stored instance
            // when adding endpoints or removing private keys
            var actorJson = JsonSerializer.Serialize(actor, _jsonOptions);
            actor = JsonSerializer.Deserialize<Actor>(actorJson, _jsonOptions)!;

            // Add endpoints property to advertise capabilities
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            actor.ExtensionData ??= new Dictionary<string, JsonElement>();
            
            actor.ExtensionData["endpoints"] = JsonSerializer.SerializeToElement(new
            {
                sharedInbox = $"{baseUrl}/inbox",
                uploadMedia = $"{baseUrl}/users/{username}/media"
            }, _jsonOptions);

            // Add custom collections metadata for public collections
            try
            {
                var collections = await _collectionService.GetCollectionDefinitionsAsync(username);
                var publicCollections = collections.Where(c => c.Visibility == CollectionVisibility.Public).ToList();
                
                if (publicCollections.Any())
                {
                    // Add collections endpoint
                    var collectionsData = new Dictionary<string, object>
                    {
                        ["collections"] = $"{baseUrl}/users/{username}/collections"
                    };
                    
                    // Add individual collection links as extension properties
                    foreach (var collection in publicCollections)
                    {
                        collectionsData[collection.Id] = $"{baseUrl}/users/{username}/collections/{collection.Id}";
                    }
                    
                    actor.ExtensionData["broca:collections"] = JsonSerializer.SerializeToElement(collectionsData, _jsonOptions);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add collections metadata to actor {Username}", username);
                // Don't fail the request if collections metadata fails
            }

            // Check if admin token is provided and valid
            bool includePrivateKey = IsAdminTokenValid();
            
            _logger.LogDebug("Actor {Username} request - AdminApiToken configured: {HasToken}, Include private key: {IncludeKey}", 
                username, 
                !string.IsNullOrWhiteSpace(_options.AdminApiToken),
                includePrivateKey);
            
            if (!includePrivateKey)
            {
                // Remove private key from response for non-admin requests
                if (actor.ExtensionData?.ContainsKey("privateKeyPem") == true)
                {
                    _logger.LogDebug("Removing private key from actor {Username} response", username);
                    actor.ExtensionData.Remove("privateKeyPem");
                }
            }

            return Ok(actor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving actor {Username}", username);
            return StatusCode(500, new { error = "Internal server error" });
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
            _logger.LogDebug("AdminApiToken not configured");
            return false;
        }

        // Extract token from Authorization header
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            _logger.LogDebug("No Authorization header provided");
            return false;
        }

        // Check for Bearer token format
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Authorization header does not start with 'Bearer '");
            return false;
        }

        var providedToken = authHeader.Substring(7).Trim();
        
        // Constant-time comparison to prevent timing attacks
        var isValid = CryptographicEquals(providedToken, _options.AdminApiToken);
        _logger.LogDebug("API token validation result: {IsValid}", isValid);
        return isValid;
    }

    /// <summary>
    /// Performs constant-time string comparison to prevent timing attacks
    /// </summary>
    private static bool CryptographicEquals(string a, string b)
    {
        if (a == null || b == null)
        {
            return a == b;
        }

        if (a.Length != b.Length)
        {
            return false;
        }

        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }

    [HttpGet("followers")]
    [Produces("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> GetFollowers(string username)
    {
        try
        {
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            var followers = await _actorRepository.GetFollowersAsync(username);
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var collection = new OrderedCollection
            {
                JsonLDContext = new List<ITermDefinition> 
                { 
                    new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) 
                },
                Id = $"{baseUrl}/users/{username}/followers",
                TotalItems = (uint)followers.Count(),
                OrderedItems = followers.Select(f => new Link { Href = new Uri(f) } as IObjectOrLink).ToList()
            };

            return Ok(collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving followers for {Username}", username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("following")]
    [Produces("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> GetFollowing(string username)
    {
        try
        {
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            var following = await _actorRepository.GetFollowingAsync(username);
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var collection = new OrderedCollection
            {
                JsonLDContext = new List<ITermDefinition> 
                { 
                    new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) 
                },
                Id = $"{baseUrl}/users/{username}/following",
                TotalItems = (uint)following.Count(),
                OrderedItems = following.Select(f => new Link { Href = new Uri(f) } as IObjectOrLink).ToList()
            };

            return Ok(collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving following for {Username}", username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("liked")]
    [Produces("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> GetLiked(string username, [FromQuery] int page = 0, [FromQuery] int limit = 20)
    {
        try
        {
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            var offset = page * limit;
            var liked = await _activityRepository.GetLikedByActorAsync(username, limit, offset);
            var totalCount = await _activityRepository.GetLikedByActorCountAsync(username);
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
                    Id = $"{baseUrl}/users/{username}/liked",
                    TotalItems = (uint)totalCount,
                    First = totalCount > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/liked?page=0&limit={limit}") } 
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
                    Id = $"{baseUrl}/users/{username}/liked?page={page}&limit={limit}",
                    PartOf = new Link { Href = new Uri($"{baseUrl}/users/{username}/liked") },
                    TotalItems = (uint)totalCount,
                    OrderedItems = liked.ToList(),
                    Next = (offset + limit < totalCount) 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/liked?page={page + 1}&limit={limit}") }
                        : null,
                    Prev = page > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/liked?page={page - 1}&limit={limit}") }
                        : null
                };
                return Ok(collectionPage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving liked for {Username}", username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("shared")]
    [Produces("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> GetShared(string username, [FromQuery] int page = 0, [FromQuery] int limit = 20)
    {
        try
        {
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            var offset = page * limit;
            var shared = await _activityRepository.GetSharedByActorAsync(username, limit, offset);
            var totalCount = await _activityRepository.GetSharedByActorCountAsync(username);
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
                    Id = $"{baseUrl}/users/{username}/shared",
                    TotalItems = (uint)totalCount,
                    First = totalCount > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/shared?page=0&limit={limit}") } 
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
                    Id = $"{baseUrl}/users/{username}/shared?page={page}&limit={limit}",
                    PartOf = new Link { Href = new Uri($"{baseUrl}/users/{username}/shared") },
                    TotalItems = (uint)totalCount,
                    OrderedItems = shared.ToList(),
                    Next = (offset + limit < totalCount) 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/shared?page={page + 1}&limit={limit}") }
                        : null,
                    Prev = page > 0 
                        ? new Link { Href = new Uri($"{baseUrl}/users/{username}/shared?page={page - 1}&limit={limit}") }
                        : null
                };
                return Ok(collectionPage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shared for {Username}", username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
