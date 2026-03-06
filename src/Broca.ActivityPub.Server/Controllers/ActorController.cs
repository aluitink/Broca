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
public class ActorController : ActivityPubControllerBase
{
    private readonly IActorRepository _actorRepository;
    private readonly IActivityRepository _activityRepository;
    private readonly ICollectionService _collectionService;
    private readonly ObjectEnrichmentService _enrichmentService;
    private readonly IdentityProviderService? _identityProviderService;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<ActorController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ActorController(
        IActorRepository actorRepository,
        IActivityRepository activityRepository,
        ICollectionService collectionService,
        ObjectEnrichmentService enrichmentService,
        IOptions<ActivityPubServerOptions> options,
        ILogger<ActorController> logger,
        IdentityProviderService? identityProviderService = null)
    {
        _actorRepository = actorRepository;
        _activityRepository = activityRepository;
        _collectionService = collectionService;
        _enrichmentService = enrichmentService;
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
            var baseUrl = GetBaseUrl(_options.NormalizedRoutePrefix);
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
                    // Collections catalog pointer — broca: prefixed so it's scoped to our namespace
                    actor.ExtensionData["broca:collections"] = JsonSerializer.SerializeToElement(
                        $"{baseUrl}/users/{username}/collections",
                        _jsonOptions);
                    
                    // Plain "featured" kept for Mastodon/Pleroma interoperability (de facto AP standard for pinned posts)
                    var featuredCollection = publicCollections.FirstOrDefault(c => c.Id == "featured");
                    if (featuredCollection != null)
                    {
                        actor.ExtensionData["featured"] = JsonSerializer.SerializeToElement(
                            $"{baseUrl}/users/{username}/collections/featured",
                            _jsonOptions);
                    }
                    
                    // All individual collections with broca: prefix
                    foreach (var collection in publicCollections)
                    {
                        actor.ExtensionData[$"broca:{collection.Id}"] = JsonSerializer.SerializeToElement(
                            $"{baseUrl}/users/{username}/collections/{collection.Id}",
                            _jsonOptions);
                    }

                    // Declare the broca: namespace in @context so JSON-LD processors can resolve it
                    actor.JsonLDContext ??= new List<ITermDefinition>();
                    var contextList = actor.JsonLDContext.ToList();
                    var brocaNsUri = new Uri($"{baseUrl}/ns/broca");
                    if (!contextList.OfType<ReferenceTermDefinition>().Any(r => r.Href == brocaNsUri))
                    {
                        contextList.Add(new ReferenceTermDefinition(brocaNsUri));
                        actor.JsonLDContext = contextList;
                    }
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
    public async Task<IActionResult> GetFollowers(string username, [FromQuery] int page = 0, [FromQuery] int limit = 20)
    {
        try
        {
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            var baseUrl = GetBaseUrl(_options.NormalizedRoutePrefix);
            var collectionUrl = $"{baseUrl}/users/{username}/followers";
            var totalCount = await _actorRepository.GetFollowersCountAsync(username);
            var hasPageParam = Request.Query.ContainsKey("page") || Request.Query.ContainsKey("limit");
            IEnumerable<IObjectOrLink> items = hasPageParam
                ? (await _actorRepository.GetFollowersAsync(username, limit, page * limit))
                    .Select(f => (IObjectOrLink)new Link { Href = new Uri(f) })
                : Enumerable.Empty<IObjectOrLink>();
            return BuildCollectionResponse(collectionUrl, items, totalCount, page, limit, itemsAlreadyPaginated: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving followers for {Username}", username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("following")]
    [Produces("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> GetFollowing(string username, [FromQuery] int page = 0, [FromQuery] int limit = 20)
    {
        try
        {
            var actor = await _actorRepository.GetActorByUsernameAsync(username);
            if (actor == null)
            {
                return NotFound(new { error = "Actor not found" });
            }

            var baseUrl = GetBaseUrl(_options.NormalizedRoutePrefix);
            var collectionUrl = $"{baseUrl}/users/{username}/following";
            var totalCount = await _actorRepository.GetFollowingCountAsync(username);
            var hasPageParam = Request.Query.ContainsKey("page") || Request.Query.ContainsKey("limit");
            IEnumerable<IObjectOrLink> items = hasPageParam
                ? (await _actorRepository.GetFollowingAsync(username, limit, page * limit))
                    .Select(f => (IObjectOrLink)new Link { Href = new Uri(f) })
                : Enumerable.Empty<IObjectOrLink>();
            return BuildCollectionResponse(collectionUrl, items, totalCount, page, limit, itemsAlreadyPaginated: true);
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

            var search = GetSearchParameters();
            var offset = page * limit;
            var baseUrl = GetBaseUrl(_options.NormalizedRoutePrefix);

            IEnumerable<IObjectOrLink> liked;
            int likedCount;
            bool likedAlreadyPaginated;

            if (search?.HasSearchCriteria == true)
            {
                liked = await _activityRepository.GetLikedByActorAsync(username, int.MaxValue, 0);
                likedCount = liked.Count();
                likedAlreadyPaginated = false;
            }
            else
            {
                liked = await _activityRepository.GetLikedByActorAsync(username, limit, offset);
                likedCount = await _activityRepository.GetLikedByActorCountAsync(username);
                likedAlreadyPaginated = true;
            }

            await _enrichmentService.EnrichActivitiesAsync(liked, baseUrl);

            var collectionUrl = $"{baseUrl}/users/{username}/liked";
            return BuildCollectionResponse(collectionUrl, liked, likedCount, page, limit, search, likedAlreadyPaginated);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { error = $"Invalid search parameter: {ex.Message}" });
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

            var search = GetSearchParameters();
            var offset = page * limit;
            var baseUrl = GetBaseUrl(_options.NormalizedRoutePrefix);

            IEnumerable<IObjectOrLink> shared;
            int sharedCount;
            bool sharedAlreadyPaginated;

            if (search?.HasSearchCriteria == true)
            {
                shared = await _activityRepository.GetSharedByActorAsync(username, int.MaxValue, 0);
                sharedCount = shared.Count();
                sharedAlreadyPaginated = false;
            }
            else
            {
                shared = await _activityRepository.GetSharedByActorAsync(username, limit, offset);
                sharedCount = await _activityRepository.GetSharedByActorCountAsync(username);
                sharedAlreadyPaginated = true;
            }

            await _enrichmentService.EnrichActivitiesAsync(shared, baseUrl);

            var collectionUrl = $"{baseUrl}/users/{username}/shared";
            return BuildCollectionResponse(collectionUrl, shared, sharedCount, page, limit, search, sharedAlreadyPaginated);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { error = $"Invalid search parameter: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shared for {Username}", username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
