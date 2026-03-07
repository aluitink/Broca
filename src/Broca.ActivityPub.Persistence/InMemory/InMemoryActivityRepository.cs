using System.Collections.Concurrent;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Persistence.InMemory;

/// <summary>
/// In-memory implementation of activity repository for testing and development
/// </summary>
public class InMemoryActivityRepository : IActivityRepository, IActivityStatistics, ISearchableActivityRepository
{
    private readonly ConcurrentDictionary<string, List<(string Id, IObjectOrLink Activity, DateTime Timestamp)>> _inboxes = new();
    private readonly ConcurrentDictionary<string, List<(string Id, IObjectOrLink Activity, DateTime Timestamp)>> _outboxes = new();
    private readonly ConcurrentDictionary<string, IObjectOrLink> _activities = new();
    
    // Index for tracking likes: objectId -> list of Like activities
    private readonly ConcurrentDictionary<string, List<(string ActivityId, string ActorUsername, DateTime Timestamp)>> _likes = new();
    
    // Index for tracking shares/announces: objectId -> list of Announce activities
    private readonly ConcurrentDictionary<string, List<(string ActivityId, string ActorUsername, DateTime Timestamp)>> _shares = new();
    
    // Index for tracking replies: objectId -> list of reply activities
    private readonly ConcurrentDictionary<string, List<(string ActivityId, DateTime Timestamp)>> _replies = new();
    
    private readonly ICollectionSearchEngine? _searchEngine;
    private readonly ILogger<InMemoryActivityRepository>? _logger;

    public InMemoryActivityRepository(ILogger<InMemoryActivityRepository>? logger = null, ICollectionSearchEngine? searchEngine = null)
    {
        _logger = logger;
        _searchEngine = searchEngine;
    }

    public Task SaveInboxActivityAsync(string username, string activityId, IObjectOrLink activity, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        var inbox = _inboxes.GetOrAdd(key, _ => new List<(string, IObjectOrLink, DateTime)>());
        
        _logger?.LogDebug("SaveInboxActivityAsync: Saving activity to {Username}'s inbox. ActivityId={ActivityId}, ConcreteType={ConcreteType}",
            username, activityId, activity.GetType().Name);
        
        lock (inbox)
        {
            inbox.Add((activityId, activity, DateTime.UtcNow));
        }
        
        _activities[activityId] = activity;
        
        // Index the activity for querying
        IndexActivity(activity, activityId, username);
        
        return Task.CompletedTask;
    }

    public Task SaveOutboxActivityAsync(string username, string activityId, IObjectOrLink activity, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        var outbox = _outboxes.GetOrAdd(key, _ => new List<(string, IObjectOrLink, DateTime)>());
        
        lock (outbox)
        {
            outbox.Add((activityId, activity, DateTime.UtcNow));
        }
        
        _activities[activityId] = activity;
        
        // Index the activity for querying
        IndexActivity(activity, activityId, username);
        
        return Task.CompletedTask;
    }

    public Task<IEnumerable<IObjectOrLink>> GetInboxActivitiesAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        if (_inboxes.TryGetValue(key, out var inbox))
        {
            lock (inbox)
            {
                var activities = inbox
                    .OrderByDescending(x => x.Timestamp)
                    .Skip(offset)
                    .Take(limit)
                    .Select(x => x.Activity)
                    .ToList();
                
                _logger?.LogDebug("GetInboxActivitiesAsync: Retrieved {Count} activities from {Username}'s inbox. Types: {Types}",
                    activities.Count, username, string.Join(", ", activities.Select(a => a.GetType().Name)));
                
                return Task.FromResult<IEnumerable<IObjectOrLink>>(activities);
            }
        }
        
        _logger?.LogDebug("GetInboxActivitiesAsync: No inbox found for {Username}", username);
        return Task.FromResult<IEnumerable<IObjectOrLink>>(Array.Empty<IObjectOrLink>());
    }

    public Task<IEnumerable<IObjectOrLink>> GetOutboxActivitiesAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        if (_outboxes.TryGetValue(key, out var outbox))
        {
            lock (outbox)
            {
                // Filter to only return Activities (not bare Objects like Note, Article, etc.)
                // Per ActivityPub spec, outbox should only contain Activities
                var activities = outbox
                    .Where(x => x.Activity is Activity)  // Only include Activity types
                    .OrderByDescending(x => x.Timestamp)
                    .Skip(offset)
                    .Take(limit)
                    .Select(x => x.Activity)
                    .ToList();
                return Task.FromResult<IEnumerable<IObjectOrLink>>(activities);
            }
        }
        return Task.FromResult<IEnumerable<IObjectOrLink>>(Array.Empty<IObjectOrLink>());
    }

    public Task<IObjectOrLink?> GetActivityByIdAsync(string activityId, CancellationToken cancellationToken = default)
    {
        _activities.TryGetValue(activityId, out var activity);
        return Task.FromResult(activity);
    }

    public Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
    {
        if (_activities.TryGetValue(activityId, out var activity))
        {
            // Remove from index before deleting
            RemoveFromIndex(activity, activityId);
        }
        
        _activities.TryRemove(activityId, out _);
        
        // Remove from all inboxes and outboxes
        foreach (var inbox in _inboxes.Values)
        {
            lock (inbox)
            {
                inbox.RemoveAll(x => x.Id == activityId);
            }
        }
        
        foreach (var outbox in _outboxes.Values)
        {
            lock (outbox)
            {
                outbox.RemoveAll(x => x.Id == activityId);
            }
        }
        
        return Task.CompletedTask;
    }

    public Task<int> GetInboxCountAsync(string username, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        if (_inboxes.TryGetValue(key, out var inbox))
        {
            lock (inbox)
            {
                return Task.FromResult(inbox.Count);
            }
        }
        return Task.FromResult(0);
    }

    public Task<int> GetOutboxCountAsync(string username, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        if (_outboxes.TryGetValue(key, out var outbox))
        {
            lock (outbox)
            {
                // Only count Activities, not bare Objects
                var count = outbox.Count(x => x.Activity is Activity);
                return Task.FromResult(count);
            }
        }
        return Task.FromResult(0);
    }

    public Task<IEnumerable<IObjectOrLink>> GetRepliesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        if (_replies.TryGetValue(objectId, out var replies))
        {
            lock (replies)
            {
                var replyActivities = replies
                    .OrderByDescending(x => x.Timestamp)
                    .Skip(offset)
                    .Take(limit)
                    .Select(x => _activities.TryGetValue(x.ActivityId, out var activity) ? activity : null)
                    .Where(x => x != null)
                    .Cast<IObjectOrLink>()
                    .ToList();
                
                return Task.FromResult<IEnumerable<IObjectOrLink>>(replyActivities);
            }
        }
        return Task.FromResult<IEnumerable<IObjectOrLink>>(Array.Empty<IObjectOrLink>());
    }

    public Task<int> GetRepliesCountAsync(string objectId, CancellationToken cancellationToken = default)
    {
        if (_replies.TryGetValue(objectId, out var replies))
        {
            lock (replies)
            {
                return Task.FromResult(replies.Count);
            }
        }
        return Task.FromResult(0);
    }

    public Task<IEnumerable<IObjectOrLink>> GetLikesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        if (_likes.TryGetValue(objectId, out var likes))
        {
            lock (likes)
            {
                var likeActivities = likes
                    .OrderByDescending(x => x.Timestamp)
                    .Skip(offset)
                    .Take(limit)
                    .Select(x => _activities.TryGetValue(x.ActivityId, out var activity) ? activity : null)
                    .Where(x => x != null)
                    .Cast<IObjectOrLink>()
                    .ToList();
                
                return Task.FromResult<IEnumerable<IObjectOrLink>>(likeActivities);
            }
        }
        return Task.FromResult<IEnumerable<IObjectOrLink>>(Array.Empty<IObjectOrLink>());
    }

    public Task<int> GetLikesCountAsync(string objectId, CancellationToken cancellationToken = default)
    {
        if (_likes.TryGetValue(objectId, out var likes))
        {
            lock (likes)
            {
                return Task.FromResult(likes.Count);
            }
        }
        return Task.FromResult(0);
    }

    public Task<IEnumerable<IObjectOrLink>> GetSharesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        if (_shares.TryGetValue(objectId, out var shares))
        {
            lock (shares)
            {
                var shareActivities = shares
                    .OrderByDescending(x => x.Timestamp)
                    .Skip(offset)
                    .Take(limit)
                    .Select(x => _activities.TryGetValue(x.ActivityId, out var activity) ? activity : null)
                    .Where(x => x != null)
                    .Cast<IObjectOrLink>()
                    .ToList();
                
                return Task.FromResult<IEnumerable<IObjectOrLink>>(shareActivities);
            }
        }
        return Task.FromResult<IEnumerable<IObjectOrLink>>(Array.Empty<IObjectOrLink>());
    }

    public Task<int> GetSharesCountAsync(string objectId, CancellationToken cancellationToken = default)
    {
        if (_shares.TryGetValue(objectId, out var shares))
        {
            lock (shares)
            {
                return Task.FromResult(shares.Count);
            }
        }
        return Task.FromResult(0);
    }

    public Task<IEnumerable<IObjectOrLink>> GetLikedByActorAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        var likedActivities = new List<IObjectOrLink>();
        
        foreach (var likePair in _likes)
        {
            lock (likePair.Value)
            {
                var actorLikes = likePair.Value
                    .Where(x => x.ActorUsername.Equals(key, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Timestamp)
                    .Select(x => _activities.TryGetValue(x.ActivityId, out var activity) ? activity : null)
                    .Where(x => x != null)
                    .Cast<IObjectOrLink>();
                
                likedActivities.AddRange(actorLikes);
            }
        }
        
        var result = likedActivities
            .OrderByDescending(x => GetTimestamp(x))
            .Skip(offset)
            .Take(limit)
            .ToList();
        
        return Task.FromResult<IEnumerable<IObjectOrLink>>(result);
    }

    public Task<int> GetLikedByActorCountAsync(string username, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        var count = 0;
        
        foreach (var likePair in _likes)
        {
            lock (likePair.Value)
            {
                count += likePair.Value.Count(x => x.ActorUsername.Equals(key, StringComparison.OrdinalIgnoreCase));
            }
        }
        
        return Task.FromResult(count);
    }

    public Task<IEnumerable<IObjectOrLink>> GetSharedByActorAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        var sharedActivities = new List<IObjectOrLink>();
        
        foreach (var sharePair in _shares)
        {
            lock (sharePair.Value)
            {
                var actorShares = sharePair.Value
                    .Where(x => x.ActorUsername.Equals(key, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Timestamp)
                    .Select(x => _activities.TryGetValue(x.ActivityId, out var activity) ? activity : null)
                    .Where(x => x != null)
                    .Cast<IObjectOrLink>();
                
                sharedActivities.AddRange(actorShares);
            }
        }
        
        var result = sharedActivities
            .OrderByDescending(x => GetTimestamp(x))
            .Skip(offset)
            .Take(limit)
            .ToList();
        
        return Task.FromResult<IEnumerable<IObjectOrLink>>(result);
    }

    public Task<int> GetSharedByActorCountAsync(string username, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        var count = 0;
        
        foreach (var sharePair in _shares)
        {
            lock (sharePair.Value)
            {
                count += sharePair.Value.Count(x => x.ActorUsername.Equals(key, StringComparison.OrdinalIgnoreCase));
            }
        }
        
        return Task.FromResult(count);
    }

    public Task MarkObjectAsDeletedAsync(string objectId, CancellationToken cancellationToken = default)
    {
        var tombstone = new Tombstone
        {
            Id = objectId,
            Type = new[] { "Tombstone" },
            Deleted = DateTime.UtcNow
        };
        
        _activities[objectId] = tombstone;
        return Task.CompletedTask;
    }

    public Task RecordInteractionAsync(string objectId, ActivityInteractionType type, string activityId, CancellationToken cancellationToken = default)
    {
        var timestamp = DateTime.UtcNow;
        switch (type)
        {
            case ActivityInteractionType.Like:
                var likeList = _likes.GetOrAdd(objectId, _ => new List<(string, string, DateTime)>());
                lock (likeList)
                {
                    if (!likeList.Any(x => x.ActivityId == activityId))
                        likeList.Add((activityId, activityId, timestamp));
                }
                break;
            case ActivityInteractionType.Announce:
                var shareList = _shares.GetOrAdd(objectId, _ => new List<(string, string, DateTime)>());
                lock (shareList)
                {
                    if (!shareList.Any(x => x.ActivityId == activityId))
                        shareList.Add((activityId, activityId, timestamp));
                }
                break;
            case ActivityInteractionType.Reply:
                var replyList = _replies.GetOrAdd(objectId, _ => new List<(string, DateTime)>());
                lock (replyList)
                {
                    if (!replyList.Any(x => x.ActivityId == activityId))
                        replyList.Add((activityId, timestamp));
                }
                break;
        }
        return Task.CompletedTask;
    }

    public Task RemoveInteractionAsync(string objectId, ActivityInteractionType type, string activityId, CancellationToken cancellationToken = default)
    {
        switch (type)
        {
            case ActivityInteractionType.Like:
                if (_likes.TryGetValue(objectId, out var likeList))
                    lock (likeList)
                        likeList.RemoveAll(x => x.ActivityId == activityId);
                break;
            case ActivityInteractionType.Announce:
                if (_shares.TryGetValue(objectId, out var shareList))
                    lock (shareList)
                        shareList.RemoveAll(x => x.ActivityId == activityId);
                break;
            case ActivityInteractionType.Reply:
                if (_replies.TryGetValue(objectId, out var replyList))
                    lock (replyList)
                        replyList.RemoveAll(x => x.ActivityId == activityId);
                break;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Indexes an activity for efficient querying
    /// </summary>
    private void IndexActivity(IObjectOrLink activity, string activityId, string username)
    {
        // Check if activity is IObject and has Type property
        if (activity is not IObject obj || obj.Type == null)
            return;

        var types = obj.Type;
        var timestamp = DateTime.UtcNow;
        
        // For Like and Announce activities, extract the actual actor who performed the action
        // rather than using the inbox/outbox owner
        string actorUsername = username;
        if (types.Contains("Like") || types.Contains("Announce"))
        {
            var extractedActor = ExtractActorUsername(obj);
            if (!string.IsNullOrEmpty(extractedActor))
            {
                actorUsername = extractedActor;
            }
        }

        // Index Like activities
        if (types.Contains("Like"))
        {
            var objectId = ExtractObjectId(obj);
            if (!string.IsNullOrEmpty(objectId))
            {
                var likeList = _likes.GetOrAdd(objectId, _ => new List<(string, string, DateTime)>());
                lock (likeList)
                {
                    if (!likeList.Any(x => x.ActivityId == activityId))
                    {
                        likeList.Add((activityId, actorUsername.ToLowerInvariant(), timestamp));
                    }
                }
            }
        }
        // Index Announce activities
        else if (types.Contains("Announce"))
        {
            var objectId = ExtractObjectId(obj);
            if (!string.IsNullOrEmpty(objectId))
            {
                var shareList = _shares.GetOrAdd(objectId, _ => new List<(string, string, DateTime)>());
                lock (shareList)
                {
                    if (!shareList.Any(x => x.ActivityId == activityId))
                    {
                        shareList.Add((activityId, actorUsername.ToLowerInvariant(), timestamp));
                    }
                }
            }
        }
        // Index replies (objects with inReplyTo property)
        else if (TryGetInReplyTo(obj, out var inReplyTo))
        {
            var replyList = _replies.GetOrAdd(inReplyTo, _ => new List<(string, DateTime)>());
            lock (replyList)
            {
                if (!replyList.Any(x => x.ActivityId == activityId))
                    replyList.Add((activityId, timestamp));
            }
        }
    }

    /// <summary>
    /// Removes an activity from indexes
    /// </summary>
    private void RemoveFromIndex(IObjectOrLink activity, string activityId)
    {
        if (activity is not IObject obj || obj.Type == null)
            return;

        var types = obj.Type;

        // Remove from likes index
        if (types.Contains("Like"))
        {
            var objectId = ExtractObjectId(obj);
            if (!string.IsNullOrEmpty(objectId) && _likes.TryGetValue(objectId, out var likeList))
            {
                lock (likeList)
                {
                    likeList.RemoveAll(x => x.ActivityId == activityId);
                }
            }
        }
        // Remove from shares index
        else if (types.Contains("Announce"))
        {
            var objectId = ExtractObjectId(obj);
            if (!string.IsNullOrEmpty(objectId) && _shares.TryGetValue(objectId, out var shareList))
            {
                lock (shareList)
                {
                    shareList.RemoveAll(x => x.ActivityId == activityId);
                }
            }
        }
        // Remove from replies index
        else if (TryGetInReplyTo(obj, out var inReplyTo) && _replies.TryGetValue(inReplyTo, out var replyList))
        {
            lock (replyList)
            {
                replyList.RemoveAll(x => x.ActivityId == activityId);
            }
        }
    }

    /// <summary>
    /// Extracts the object ID from an activity
    /// </summary>
    private static string? ExtractObjectId(IObject obj)
    {
        var first = (obj as Activity)?.Object?.FirstOrDefault();
        return first switch
        {
            IObject o when !string.IsNullOrEmpty(o.Id) => o.Id,
            ILink l when l.Href != null => l.Href.ToString(),
            _ => null
        };
    }

    /// <summary>
    /// Extracts the actor username from an activity
    /// </summary>
    private static string? ExtractActorUsername(IObject obj)
    {
        var firstActor = (obj as Activity)?.Actor?.FirstOrDefault();
        var actorId = firstActor switch
        {
            IObject actorObj when !string.IsNullOrEmpty(actorObj.Id) => actorObj.Id,
            ILink link when link.Href != null => link.Href.ToString(),
            _ => null
        };

        if (string.IsNullOrEmpty(actorId))
            return null;

        var lastSlash = actorId.LastIndexOf('/');
        return lastSlash >= 0 && lastSlash < actorId.Length - 1
            ? actorId[(lastSlash + 1)..]
            : null;
    }

    /// <summary>
    /// Tries to get the inReplyTo property from an object
    /// </summary>
    private static bool TryGetInReplyTo(IObject obj, out string inReplyTo)
    {
        inReplyTo = string.Empty;
        var first = obj.InReplyTo?.FirstOrDefault();
        switch (first)
        {
            case IObject o when !string.IsNullOrEmpty(o.Id):
                inReplyTo = o.Id;
                return true;
            case ILink l when l.Href != null:
                inReplyTo = l.Href.ToString()!;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Gets timestamp from an activity if available
    /// </summary>
    private static DateTime GetTimestamp(IObjectOrLink obj)
    {
        if (obj is IObject o && o.Published.HasValue)
            return o.Published.Value;
        return DateTime.MinValue;
    }

    /// <summary>
    /// Clears all stored data from the repository. Used for testing.
    /// </summary>
    public void Clear()
    {
        _inboxes.Clear();
        _outboxes.Clear();
        _activities.Clear();
        _likes.Clear();
        _shares.Clear();
        _replies.Clear();
    }

    public Task<int> CountCreateActivitiesSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var outbox in _outboxes.Values)
        {
            lock (outbox)
            {
                count += outbox.Count(entry =>
                {
                    if (entry.Activity is not Create createActivity)
                        return false;
                    
                    // Use Published date if available, otherwise fall back to storage timestamp
                    var activityDate = createActivity.Published ?? entry.Timestamp;
                    return activityDate >= since;
                });
            }
        }
        return Task.FromResult(count);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetInboxActivitiesAsync(
        string username,
        CollectionSearchParameters search,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var all = await GetInboxActivitiesAsync(username, int.MaxValue, 0, cancellationToken);
        return ApplySearch(all, search, limit, offset);
    }

    public async Task<int> GetInboxCountAsync(
        string username,
        CollectionSearchParameters search,
        CancellationToken cancellationToken = default)
    {
        var all = await GetInboxActivitiesAsync(username, int.MaxValue, 0, cancellationToken);
        return ApplySearchCount(all, search);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetOutboxActivitiesAsync(
        string username,
        CollectionSearchParameters search,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var all = await GetOutboxActivitiesAsync(username, int.MaxValue, 0, cancellationToken);
        return ApplySearch(all, search, limit, offset);
    }

    public async Task<int> GetOutboxCountAsync(
        string username,
        CollectionSearchParameters search,
        CancellationToken cancellationToken = default)
    {
        var all = await GetOutboxActivitiesAsync(username, int.MaxValue, 0, cancellationToken);
        return ApplySearchCount(all, search);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetRepliesAsync(
        string objectId,
        CollectionSearchParameters search,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var all = await GetRepliesAsync(objectId, int.MaxValue, 0, cancellationToken);
        return ApplySearch(all, search, limit, offset);
    }

    public async Task<int> GetRepliesCountAsync(
        string objectId,
        CollectionSearchParameters search,
        CancellationToken cancellationToken = default)
    {
        var all = await GetRepliesAsync(objectId, int.MaxValue, 0, cancellationToken);
        return ApplySearchCount(all, search);
    }

    private IEnumerable<IObjectOrLink> ApplySearch(
        IEnumerable<IObjectOrLink> items,
        CollectionSearchParameters search,
        int limit,
        int offset)
    {
        if (_searchEngine == null)
            return items.Skip(offset).Take(limit);

        var (filtered, _) = _searchEngine.Apply(items, search);
        return filtered.Skip(offset).Take(limit);
    }

    private int ApplySearchCount(IEnumerable<IObjectOrLink> items, CollectionSearchParameters search)
    {
        if (_searchEngine == null)
            return items.Count();

        var (_, count) = _searchEngine.Apply(items, search);
        return count;
    }

    public Task<int> CountActiveActorsSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        var activeUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _outboxes)
        {
            var username = kvp.Key;
            var outbox = kvp.Value;
            
            lock (outbox)
            {
                var hasCreateActivity = outbox.Any(entry =>
                {
                    if (entry.Activity is not Create createActivity)
                        return false;
                    
                    // Use Published date if available, otherwise fall back to storage timestamp
                    var activityDate = createActivity.Published ?? entry.Timestamp;
                    return activityDate >= since;
                });
                
                if (hasCreateActivity)
                {
                    activeUsernames.Add(username);
                }
            }
        }
        return Task.FromResult(activeUsernames.Count);
    }
}

