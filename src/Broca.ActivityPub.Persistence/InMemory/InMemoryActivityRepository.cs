using System.Collections.Concurrent;
using Broca.ActivityPub.Core.Interfaces;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Persistence.InMemory;

/// <summary>
/// In-memory implementation of activity repository for testing and development
/// </summary>
public class InMemoryActivityRepository : IActivityRepository
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

    public Task SaveInboxActivityAsync(string username, string activityId, IObjectOrLink activity, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        var inbox = _inboxes.GetOrAdd(key, _ => new List<(string, IObjectOrLink, DateTime)>());
        
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
                return Task.FromResult<IEnumerable<IObjectOrLink>>(activities);
            }
        }
        return Task.FromResult<IEnumerable<IObjectOrLink>>(Array.Empty<IObjectOrLink>());
    }

    public Task<IEnumerable<IObjectOrLink>> GetOutboxActivitiesAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        if (_outboxes.TryGetValue(key, out var outbox))
        {
            lock (outbox)
            {
                var activities = outbox
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
                return Task.FromResult(outbox.Count);
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
                    // Check if this actor already liked this object to avoid duplicates
                    if (!likeList.Any(x => x.ActorUsername == actorUsername.ToLowerInvariant() && x.ActivityId != activityId))
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
                    // Check if this actor already announced this object to avoid duplicates
                    if (!shareList.Any(x => x.ActorUsername == actorUsername.ToLowerInvariant() && x.ActivityId != activityId))
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
    private string? ExtractObjectId(IObject obj)
    {
        // Try to get the Object property using reflection
        var objectProperty = obj.GetType().GetProperty("Object");
        if (objectProperty?.GetValue(obj) is IEnumerable<IObjectOrLink?> objects)
        {
            var firstObject = objects.FirstOrDefault();
            if (firstObject != null)
            {
                // If it's an IObject with an Id
                if (firstObject is IObject objWithId && !string.IsNullOrEmpty(objWithId.Id))
                    return objWithId.Id;
                
                // If it's a Link with Href
                if (firstObject is ILink link && link.Href != null)
                    return link.Href.ToString();
            }
        }
        
        return null;
    }

    /// <summary>
    /// Extracts the actor username from an activity
    /// </summary>
    private string? ExtractActorUsername(IObject obj)
    {
        // Try to get the Actor property using reflection
        var actorProperty = obj.GetType().GetProperty("Actor");
        if (actorProperty?.GetValue(obj) is IEnumerable<IObjectOrLink?> actors)
        {
            var firstActor = actors.FirstOrDefault();
            if (firstActor != null)
            {
                string? actorId = null;
                
                // If it's an IObject with an Id
                if (firstActor is IObject actorObj && !string.IsNullOrEmpty(actorObj.Id))
                {
                    actorId = actorObj.Id;
                }
                // If it's a Link with Href
                else if (firstActor is ILink link && link.Href != null)
                {
                    actorId = link.Href.ToString();
                }
                
                // Extract username from actor ID (e.g., "https://example.com/users/bob" -> "bob")
                if (!string.IsNullOrEmpty(actorId))
                {
                    var lastSlashIndex = actorId.LastIndexOf('/');
                    if (lastSlashIndex >= 0 && lastSlashIndex < actorId.Length - 1)
                    {
                        return actorId.Substring(lastSlashIndex + 1);
                    }
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// Tries to get the inReplyTo property from an object
    /// </summary>
    private bool TryGetInReplyTo(IObject obj, out string inReplyTo)
    {
        inReplyTo = string.Empty;
        
        // Try to get the InReplyTo property using reflection
        var inReplyToProperty = obj.GetType().GetProperty("InReplyTo");
        if (inReplyToProperty?.GetValue(obj) is IEnumerable<IObjectOrLink?> replyToObjects)
        {
            var firstReplyTo = replyToObjects.FirstOrDefault();
            if (firstReplyTo != null)
            {
                // If it's an IObject with an Id
                if (firstReplyTo is IObject objWithId && !string.IsNullOrEmpty(objWithId.Id))
                {
                    inReplyTo = objWithId.Id;
                    return true;
                }
                
                // If it's a Link with Href
                if (firstReplyTo is ILink link && link.Href != null)
                {
                    inReplyTo = link.Href.ToString();
                    return true;
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// Gets timestamp from an activity if available
    /// </summary>
    private DateTime GetTimestamp(IObjectOrLink obj)
    {
        if (obj is IObject objWithProps)
        {
            var publishedProperty = objWithProps.GetType().GetProperty("Published");
            if (publishedProperty?.GetValue(objWithProps) is DateTime published)
                return published;
        }
        
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
}

