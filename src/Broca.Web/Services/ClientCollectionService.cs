using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Broca.Web.Services;

/// <summary>
/// Client-side implementation of ICollectionService that makes HTTP calls to the server API
/// </summary>
public class ClientCollectionService : ICollectionService
{
    private readonly HttpClient _httpClient;
    private readonly IActivityPubClient _activityPubClient;
    private readonly AuthenticationStateService _authState;
    private readonly ILogger<ClientCollectionService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ClientCollectionService(
        HttpClient httpClient,
        IActivityPubClient activityPubClient,
        AuthenticationStateService authState,
        ILogger<ClientCollectionService> logger)
    {
        _httpClient = httpClient;
        _activityPubClient = activityPubClient;
        _authState = authState;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    private async Task<string?> GetActorIdForUsername(string username, CancellationToken cancellationToken = default)
    {
        // If we're fetching for the current user, use their actor ID
        if (_authState.CurrentActor?.PreferredUsername == username && _authState.CurrentActorId != null)
        {
            return _authState.CurrentActorId;
        }

        // Otherwise, construct the actor URI from the base using webfinger or direct construction
        // For now, use direct construction with the standard pattern
        var baseUri = _httpClient.BaseAddress ?? new Uri("http://localhost");
        
        // Try to fetch the actor to get its proper ID
        try
        {
            var testUri = new Uri(baseUri, $"ap/users/{username}");
            var actor = await _activityPubClient.GetActorAsync(testUri, cancellationToken);
            return actor?.Id;
        }
        catch
        {
            // Fallback to constructed URI
            return $"{baseUri.Scheme}://{baseUri.Authority}/ap/users/{username}";
        }
    }

    public async Task<CustomCollectionDefinition> CreateCollectionAsync(
        string username,
        CustomCollectionDefinition definition,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a Collection object with Broca extension data
            var collection = new Collection
            {
                Type = new List<string> { "Collection" },
                Name = new List<string> { definition.Name },
                Summary = definition.Description != null ? new List<string> { definition.Description } : null,
                AttributedTo = new List<IObjectOrLink> 
                { 
                    new Link { Href = new Uri(_httpClient.BaseAddress!, $"users/{username}") } 
                },
                ExtensionData = new Dictionary<string, JsonElement>
                {
                    ["collectionDefinition"] = JsonSerializer.SerializeToElement(definition, _jsonOptions)
                }
            };

            // Use ActivityBuilder to create the Create activity
            var builder = _activityPubClient.CreateActivityBuilder();
            var createActivity = builder.Create(collection);

            // Post to the authenticated user's outbox
            var response = await _activityPubClient.PostToOutboxAsync(createActivity, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to create collection: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to create collection: {response.StatusCode}");
            }

            return definition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating collection {CollectionId} for {Username}", definition.Id, username);
            throw;
        }
    }

    public async Task<CustomCollectionDefinition> UpdateCollectionAsync(
        string username,
        string collectionId,
        CustomCollectionDefinition definition,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create an updated Collection object
            var collection = new Collection
            {
                Id = $"{_httpClient.BaseAddress}users/{username}/collections/{collectionId}",
                Type = new List<string> { "Collection" },
                Name = new List<string> { definition.Name },
                Summary = definition.Description != null ? new List<string> { definition.Description } : null,
                ExtensionData = new Dictionary<string, JsonElement>
                {
                    ["collectionDefinition"] = JsonSerializer.SerializeToElement(definition, _jsonOptions)
                }
            };

            // Use ActivityBuilder to create the Update activity
            var builder = _activityPubClient.CreateActivityBuilder();
            var updateActivity = builder.Update(collection);

            // Post to outbox
            var response = await _activityPubClient.PostToOutboxAsync(updateActivity, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to update collection: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to update collection: {response.StatusCode}");
            }

            return definition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating collection {CollectionId} for {Username}", collectionId, username);
            throw;
        }
    }

    public async Task DeleteCollectionAsync(
        string username,
        string collectionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collectionUrl = $"{_httpClient.BaseAddress}users/{username}/collections/{collectionId}";
            
            // Use ActivityBuilder to create the Delete activity
            var builder = _activityPubClient.CreateActivityBuilder();
            var deleteActivity = builder.Delete(collectionUrl);

            // Post to outbox
            var response = await _activityPubClient.PostToOutboxAsync(deleteActivity, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to delete collection: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to delete collection: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting collection {CollectionId} for {Username}", collectionId, username);
            throw;
        }
    }

    public async Task<CustomCollectionDefinition?> GetCollectionDefinitionAsync(
        string username,
        string collectionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the actor ID to build the proper collection URI
            var actorId = await GetActorIdForUsername(username, cancellationToken);
            if (actorId == null)
            {
                _logger.LogWarning("Could not determine actor ID for username {Username}", username);
                return null;
            }

            // Build the definition URI based on the actor's base URI
            var actorUri = new Uri(actorId);
            var definitionUri = new Uri(actorUri.GetLeftPart(UriPartial.Path) + $"/collections/{collectionId}/definition");
            
            var response = await _activityPubClient.GetAsync<CustomCollectionDefinition>(
                definitionUri,
                cancellationToken: cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get collection definition for {Username}/{CollectionId}", username, collectionId);
            return null;
        }
    }

    public async Task<IEnumerable<CustomCollectionDefinition>> GetCollectionDefinitionsAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the actor ID to build the proper collections catalog URI
            var actorId = await GetActorIdForUsername(username, cancellationToken);
            if (actorId == null)
            {
                _logger.LogWarning("Could not determine actor ID for username {Username}", username);
                return Enumerable.Empty<CustomCollectionDefinition>();
            }

            // Build the collections catalog URI from the actor's base URI
            var actorUri = new Uri(actorId);
            var catalogUri = new Uri(actorUri.GetLeftPart(UriPartial.Path) + "/collections");
            
            var catalog = await _activityPubClient.GetAsync<OrderedCollection>(catalogUri, cancellationToken: cancellationToken);
            
            if (catalog?.OrderedItems == null)
            {
                return Enumerable.Empty<CustomCollectionDefinition>();
            }

            // For each collection in the catalog, fetch its definition
            var definitions = new List<CustomCollectionDefinition>();
            foreach (var item in catalog.OrderedItems)
            {
                // Extract collection ID from the collection URI
                string? collectionId = null;
                if (item is Collection collection && collection.Id != null)
                {
                    var uriSegments = collection.Id.Split('/');
                    collectionId = uriSegments.Last();
                }
                else if (item is Link link && link.Href != null)
                {
                    var uriSegments = link.Href.ToString().Split('/');
                    collectionId = uriSegments.Last();
                }

                if (!string.IsNullOrEmpty(collectionId))
                {
                    var definition = await GetCollectionDefinitionAsync(username, collectionId, cancellationToken);
                    if (definition != null)
                    {
                        definitions.Add(definition);
                    }
                }
            }

            return definitions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get collection definitions for {Username}", username);
            return Enumerable.Empty<CustomCollectionDefinition>();
        }
    }

    public async Task<IEnumerable<IObjectOrLink>> GetCollectionItemsAsync(
        string username,
        string collectionId,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the ActivityPub client to fetch the collection
            var collectionUri = new Uri(_httpClient.BaseAddress!, $"users/{username}/collections/{collectionId}?limit={limit}&page={offset / limit}");
            
            var items = new List<IObjectOrLink>();
            var count = 0;
            
            await foreach (var item in _activityPubClient.GetCollectionAsync<JsonElement>(collectionUri, limit, cancellationToken))
            {
                // Skip items before the offset
                if (count < offset)
                {
                    count++;
                    continue;
                }

                // Try to deserialize as an ActivityStreams object
                try
                {
                    var obj = JsonSerializer.Deserialize<IObject>(item.GetRawText(), _jsonOptions);
                    if (obj != null)
                    {
                        items.Add(obj);
                    }
                }
                catch
                {
                    // If deserialization fails, create a Link with the ID
                    if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    {
                        items.Add(new Link { Href = new Uri(id.GetString()!) });
                    }
                }

                count++;
                if (items.Count >= limit)
                    break;
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get collection items for {Username}/{CollectionId}", username, collectionId);
            return Enumerable.Empty<IObjectOrLink>();
        }
    }

    public async Task<int> GetCollectionItemCountAsync(
        string username,
        string collectionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collectionUri = new Uri(_httpClient.BaseAddress!, $"users/{username}/collections/{collectionId}");
            var collection = await _activityPubClient.GetAsync<JsonElement>(collectionUri, cancellationToken: cancellationToken);

            if (collection.TryGetProperty("totalItems", out var totalItems))
            {
                return totalItems.GetInt32();
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get collection item count for {Username}/{CollectionId}", username, collectionId);
            return 0;
        }
    }

    public async Task AddItemToCollectionAsync(
        string username,
        string collectionId,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collectionUrl = $"{_httpClient.BaseAddress}users/{username}/collections/{collectionId}";
            
            // Use ActivityBuilder to create the Add activity
            var builder = _activityPubClient.CreateActivityBuilder();
            var addActivity = builder.Add(itemId, collectionUrl);

            // Post to outbox
            var response = await _activityPubClient.PostToOutboxAsync(addActivity, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to add item to collection: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to add item to collection: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item {ItemId} to collection {CollectionId} for {Username}", itemId, collectionId, username);
            throw;
        }
    }

    public async Task RemoveItemFromCollectionAsync(
        string username,
        string collectionId,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collectionUrl = $"{_httpClient.BaseAddress}users/{username}/collections/{collectionId}";
            
            // Use ActivityBuilder to create the Remove activity
            var builder = _activityPubClient.CreateActivityBuilder();
            var removeActivity = builder.Remove(itemId, collectionUrl);

            // Post to outbox
            var response = await _activityPubClient.PostToOutboxAsync(removeActivity, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to remove item from collection: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to remove item from collection: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item {ItemId} from collection {CollectionId} for {Username}", itemId, collectionId, username);
            throw;
        }
    }

    public Task<(bool IsValid, string? ErrorMessage)> ValidateCollectionDefinitionAsync(
        CustomCollectionDefinition definition,
        CancellationToken cancellationToken = default)
    {
        // Client-side validation (matches server-side)
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            return Task.FromResult<(bool, string?)>((false, "Collection ID is required"));
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            return Task.FromResult<(bool, string?)>((false, "Collection name is required"));
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(definition.Id, @"^[a-z0-9][a-z0-9_-]*$"))
        {
            return Task.FromResult<(bool, string?)>((false, "Collection ID must start with a letter or number and contain only lowercase letters, numbers, hyphens, and underscores"));
        }

        if (definition.Id.Length > 64)
        {
            return Task.FromResult<(bool, string?)>((false, "Collection ID must be 64 characters or less"));
        }

        var reservedNames = new[] { "inbox", "outbox", "followers", "following", "liked", "shares", "collections", "endpoints" };
        if (reservedNames.Contains(definition.Id.ToLowerInvariant()))
        {
            return Task.FromResult<(bool, string?)>((false, $"Collection ID '{definition.Id}' is reserved and cannot be used"));
        }

        if (definition.Type == CollectionType.Query && definition.QueryFilter == null)
        {
            return Task.FromResult<(bool, string?)>((false, "Query collections must have a QueryFilter defined"));
        }

        if (definition.Type == CollectionType.Manual && definition.QueryFilter != null)
        {
            return Task.FromResult<(bool, string?)>((false, "Manual collections should not have a QueryFilter"));
        }

        return Task.FromResult<(bool, string?)>((true, null));
    }
}
