using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Service for managing custom collections
/// </summary>
public class CollectionService : ICollectionService
{
    private readonly IActorRepository _actorRepository;
    private readonly IActivityRepository _activityRepository;
    private readonly ILogger<CollectionService> _logger;

    public CollectionService(
        IActorRepository actorRepository,
        IActivityRepository activityRepository,
        ILogger<CollectionService> logger)
    {
        _actorRepository = actorRepository;
        _activityRepository = activityRepository;
        _logger = logger;
    }

    public async Task<CustomCollectionDefinition> CreateCollectionAsync(
        string username, 
        CustomCollectionDefinition definition, 
        CancellationToken cancellationToken = default)
    {
        // Validate the definition
        var (isValid, errorMessage) = await ValidateCollectionDefinitionAsync(definition, cancellationToken);
        if (!isValid)
        {
            throw new ArgumentException($"Invalid collection definition: {errorMessage}");
        }

        // Check if collection already exists
        var existing = await _actorRepository.GetCollectionDefinitionAsync(username, definition.Id, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Collection '{definition.Id}' already exists");
        }

        // Set timestamps
        definition.Created = DateTimeOffset.UtcNow;
        definition.Updated = DateTimeOffset.UtcNow;

        // Save the collection
        await _actorRepository.SaveCollectionDefinitionAsync(username, definition, cancellationToken);
        
        _logger.LogInformation("Created collection {CollectionId} for {Username}", definition.Id, username);
        
        return definition;
    }

    public async Task<CustomCollectionDefinition> UpdateCollectionAsync(
        string username, 
        string collectionId, 
        CustomCollectionDefinition definition, 
        CancellationToken cancellationToken = default)
    {
        // Validate the definition
        var (isValid, errorMessage) = await ValidateCollectionDefinitionAsync(definition, cancellationToken);
        if (!isValid)
        {
            throw new ArgumentException($"Invalid collection definition: {errorMessage}");
        }

        // Check if collection exists
        var existing = await _actorRepository.GetCollectionDefinitionAsync(username, collectionId, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException($"Collection '{collectionId}' not found");
        }

        // Preserve creation date and items if manual
        definition.Created = existing.Created;
        definition.Updated = DateTimeOffset.UtcNow;
        
        // If updating a manual collection, preserve items unless explicitly changed
        if (definition.Type == CollectionType.Manual && definition.Items.Count == 0 && existing.Items.Count > 0)
        {
            definition.Items = existing.Items;
        }

        // Save the updated collection
        await _actorRepository.SaveCollectionDefinitionAsync(username, definition, cancellationToken);
        
        _logger.LogInformation("Updated collection {CollectionId} for {Username}", definition.Id, username);
        
        return definition;
    }

    public async Task DeleteCollectionAsync(
        string username, 
        string collectionId, 
        CancellationToken cancellationToken = default)
    {
        await _actorRepository.DeleteCollectionDefinitionAsync(username, collectionId, cancellationToken);
        _logger.LogInformation("Deleted collection {CollectionId} for {Username}", collectionId, username);
    }

    public async Task<CustomCollectionDefinition?> GetCollectionDefinitionAsync(
        string username, 
        string collectionId, 
        CancellationToken cancellationToken = default)
    {
        return await _actorRepository.GetCollectionDefinitionAsync(username, collectionId, cancellationToken);
    }

    public async Task<IEnumerable<CustomCollectionDefinition>> GetCollectionDefinitionsAsync(
        string username, 
        CancellationToken cancellationToken = default)
    {
        return await _actorRepository.GetCollectionDefinitionsAsync(username, cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetCollectionItemsAsync(
        string username, 
        string collectionId, 
        int limit = 20, 
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var definition = await _actorRepository.GetCollectionDefinitionAsync(username, collectionId, cancellationToken);
        if (definition == null)
        {
            return Array.Empty<IObjectOrLink>();
        }

        IEnumerable<IObjectOrLink> items;

        if (definition.Type == CollectionType.Manual)
        {
            // For manual collections, retrieve items by their IDs
            items = await GetItemsByIdsAsync(username, definition.Items, cancellationToken);
        }
        else
        {
            // For query collections, apply filters
            items = await ExecuteQueryAsync(username, definition.QueryFilter, cancellationToken);
        }

        // Apply sorting
        items = ApplySorting(items, definition.SortOrder);

        // Apply max items limit if specified
        if (definition.MaxItems.HasValue)
        {
            items = items.Take(definition.MaxItems.Value);
        }

        // Apply pagination
        items = items.Skip(offset).Take(limit);

        return items;
    }

    public async Task<int> GetCollectionItemCountAsync(
        string username, 
        string collectionId, 
        CancellationToken cancellationToken = default)
    {
        var definition = await _actorRepository.GetCollectionDefinitionAsync(username, collectionId, cancellationToken);
        if (definition == null)
        {
            return 0;
        }

        if (definition.Type == CollectionType.Manual)
        {
            var count = definition.Items.Count;
            if (definition.MaxItems.HasValue)
            {
                count = Math.Min(count, definition.MaxItems.Value);
            }
            return count;
        }
        else
        {
            var items = await ExecuteQueryAsync(username, definition.QueryFilter, cancellationToken);
            var count = items.Count();
            if (definition.MaxItems.HasValue)
            {
                count = Math.Min(count, definition.MaxItems.Value);
            }
            return count;
        }
    }

    public async Task AddItemToCollectionAsync(
        string username, 
        string collectionId, 
        string itemId, 
        CancellationToken cancellationToken = default)
    {
        var definition = await _actorRepository.GetCollectionDefinitionAsync(username, collectionId, cancellationToken);
        if (definition == null)
        {
            throw new InvalidOperationException($"Collection '{collectionId}' not found");
        }

        if (definition.Type != CollectionType.Manual)
        {
            throw new InvalidOperationException($"Cannot manually add items to query collection '{collectionId}'");
        }

        await _actorRepository.AddToCollectionAsync(username, collectionId, itemId, cancellationToken);
        _logger.LogInformation("Added item {ItemId} to collection {CollectionId} for {Username}", itemId, collectionId, username);
    }

    public async Task RemoveItemFromCollectionAsync(
        string username, 
        string collectionId, 
        string itemId, 
        CancellationToken cancellationToken = default)
    {
        var definition = await _actorRepository.GetCollectionDefinitionAsync(username, collectionId, cancellationToken);
        if (definition == null)
        {
            throw new InvalidOperationException($"Collection '{collectionId}' not found");
        }

        if (definition.Type != CollectionType.Manual)
        {
            throw new InvalidOperationException($"Cannot manually remove items from query collection '{collectionId}'");
        }

        await _actorRepository.RemoveFromCollectionAsync(username, collectionId, itemId, cancellationToken);
        _logger.LogInformation("Removed item {ItemId} from collection {CollectionId} for {Username}", itemId, collectionId, username);
    }

    public Task<(bool IsValid, string? ErrorMessage)> ValidateCollectionDefinitionAsync(
        CustomCollectionDefinition definition, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            return Task.FromResult<(bool, string?)>((false, "Collection ID is required"));
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            return Task.FromResult<(bool, string?)>((false, "Collection name is required"));
        }

        // Validate collection ID format (alphanumeric, hyphens, underscores only)
        if (!System.Text.RegularExpressions.Regex.IsMatch(definition.Id, @"^[a-z0-9][a-z0-9_-]*$"))
        {
            return Task.FromResult<(bool, string?)>((false, "Collection ID must start with a letter or number and contain only lowercase letters, numbers, hyphens, and underscores"));
        }

        // Validate ID length (reasonable for URLs)
        if (definition.Id.Length > 64)
        {
            return Task.FromResult<(bool, string?)>((false, "Collection ID must be 64 characters or less"));
        }

        // Prevent reserved collection names that could conflict with standard ActivityPub properties
        var reservedNames = new[] { "inbox", "outbox", "followers", "following", "liked", "shares", "collections", "endpoints" };
        if (reservedNames.Contains(definition.Id.ToLowerInvariant()))
        {
            return Task.FromResult<(bool, string?)>((false, $"Collection ID '{definition.Id}' is reserved and cannot be used"));
        }

        // Validate query collection has a filter
        if (definition.Type == CollectionType.Query && definition.QueryFilter == null)
        {
            return Task.FromResult<(bool, string?)>((false, "Query collections must have a QueryFilter defined"));
        }

        // Validate manual collection doesn't have a filter
        if (definition.Type == CollectionType.Manual && definition.QueryFilter != null)
        {
            return Task.FromResult<(bool, string?)>((false, "Manual collections should not have a QueryFilter"));
        }

        return Task.FromResult<(bool, string?)>((true, null));
    }

    // Private helper methods

    private async Task<IEnumerable<IObjectOrLink>> GetItemsByIdsAsync(
        string username, 
        List<string> itemIds, 
        CancellationToken cancellationToken)
    {
        var items = new List<IObjectOrLink>();
        
        // Get all outbox activities for the user
        var allActivities = await _activityRepository.GetOutboxActivitiesAsync(username, int.MaxValue, 0, cancellationToken);
        
        // Filter to only the requested IDs and unwrap Create activities
        foreach (var activity in allActivities)
        {
            var id = (activity as IObject)?.Id;
            if (id != null && itemIds.Contains(id))
            {
                // Unwrap Create activities to get the underlying object
                if (activity is Create createActivity)
                {
                    var obj = createActivity.Object?.FirstOrDefault();
                    if (obj != null)
                    {
                        items.Add(obj);
                    }
                }
                else
                {
                    // For non-Create activities, add as-is
                    items.Add(activity);
                }
            }
        }
        
        return items;
    }

    private async Task<IEnumerable<IObjectOrLink>> ExecuteQueryAsync(
        string username, 
        CollectionQueryFilter? filter, 
        CancellationToken cancellationToken)
    {
        if (filter == null)
        {
            return Array.Empty<IObjectOrLink>();
        }

        // Get all outbox activities for the user
        var allActivities = await _activityRepository.GetOutboxActivitiesAsync(username, int.MaxValue, 0, cancellationToken);
        
        // Apply filters
        var filteredActivities = allActivities.AsEnumerable();

        // Filter by activity type
        if (filter.ActivityTypes != null && filter.ActivityTypes.Any())
        {
            filteredActivities = filteredActivities.Where(a =>
            {
                var activityType = (a as IObject)?.Type?.FirstOrDefault();
                return activityType != null && filter.ActivityTypes.Contains(activityType);
            });
        }

        // Filter by object type
        if (filter.ObjectTypes != null && filter.ObjectTypes.Any())
        {
            filteredActivities = filteredActivities.Where(a =>
            {
                if (a is Activity activity && activity.Object != null)
                {
                    var obj = activity.Object.FirstOrDefault() as IObject;
                    var objectType = obj?.Type?.FirstOrDefault();
                    return objectType != null && filter.ObjectTypes.Contains(objectType);
                }
                return false;
            });
        }

        // Filter by date range
        if (filter.AfterDate.HasValue)
        {
            filteredActivities = filteredActivities.Where(a =>
            {
                var published = (a as IObject)?.Published;
                return published.HasValue && published.Value >= filter.AfterDate.Value;
            });
        }

        if (filter.BeforeDate.HasValue)
        {
            filteredActivities = filteredActivities.Where(a =>
            {
                var published = (a as IObject)?.Published;
                return published.HasValue && published.Value <= filter.BeforeDate.Value;
            });
        }

        // Filter by attachment presence
        if (filter.HasAttachment.HasValue)
        {
            filteredActivities = filteredActivities.Where(a =>
            {
                var hasAttachment = (a as IObject)?.Attachment?.Any() == true;
                return hasAttachment == filter.HasAttachment.Value;
            });
        }

        // Filter by reply status
        if (filter.IsReply.HasValue)
        {
            filteredActivities = filteredActivities.Where(a =>
            {
                var isReply = (a as IObject)?.InReplyTo != null;
                return isReply == filter.IsReply.Value;
            });
        }

        // Filter by tags
        if (filter.Tags != null && filter.Tags.Any())
        {
            filteredActivities = filteredActivities.Where(a =>
            {
                var obj = a as IObject;
                if (obj?.Tag == null)
                {
                    return false;
                }

                // Check if any of the filter tags match the object's tags
                foreach (var tagItem in obj.Tag)
                {
                    if (tagItem is IObject tagObj && tagObj.Name != null)
                    {
                        if (filter.Tags.Any(t => tagObj.Name.Contains(t)))
                        {
                            return true;
                        }
                    }
                }
                return false;
            });
        }

        // Search query (simple content matching)
        if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
        {
            var searchLower = filter.SearchQuery.ToLowerInvariant();
            filteredActivities = filteredActivities.Where(a =>
            {
                var obj = a as IObject;
                var content = obj?.Content?.FirstOrDefault()?.ToString() ?? "";
                var name = obj?.Name?.FirstOrDefault() ?? "";
                var summary = obj?.Summary?.FirstOrDefault()?.ToString() ?? "";
                
                return content.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                       name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                       summary.Contains(searchLower, StringComparison.OrdinalIgnoreCase);
            });
        }

        // Unwrap Create activities to return the underlying objects
        // Collections should contain objects (Note, Article, etc.), not the Create activities that wrap them
        var unwrappedItems = new List<IObjectOrLink>();
        foreach (var activity in filteredActivities)
        {
            if (activity is Create createActivity)
            {
                var obj = createActivity.Object?.FirstOrDefault();
                if (obj != null)
                {
                    unwrappedItems.Add(obj);
                }
            }
            else
            {
                // For non-Create activities, add as-is
                unwrappedItems.Add(activity);
            }
        }

        return unwrappedItems;
    }

    private IEnumerable<IObjectOrLink> ApplySorting(
        IEnumerable<IObjectOrLink> items, 
        CollectionSortOrder sortOrder)
    {
        return sortOrder switch
        {
            CollectionSortOrder.Chronological => items.OrderByDescending(i => (i as IObject)?.Published ?? DateTimeOffset.MinValue),
            CollectionSortOrder.ReverseChronological => items.OrderBy(i => (i as IObject)?.Published ?? DateTimeOffset.MinValue),
            CollectionSortOrder.Manual => items, // Keep original order
            _ => items
        };
    }
}
