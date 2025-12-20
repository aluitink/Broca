using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Persistence.EntityFramework.Entities;
using Broca.ActivityPub.Persistence.EntityFramework.Services;
using KristofferStrube.ActivityStreams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Broca.ActivityPub.Persistence.EntityFramework.Repositories;

/// <summary>
/// Entity Framework implementation of IActivityRepository
/// </summary>
public class EfActivityRepository : IActivityRepository
{
    private readonly ActivityPubDbContext _context;
    private readonly ILogger<EfActivityRepository> _logger;
    private readonly ActivityStreamExtractor _extractor;
    private readonly CountManager _countManager;

    public EfActivityRepository(
        ActivityPubDbContext context, 
        ILogger<EfActivityRepository> logger,
        ActivityStreamExtractor extractor,
        CountManager countManager)
    {
        _context = context;
        _logger = logger;
        _extractor = extractor;
        _countManager = countManager;
    }

    public async Task SaveInboxActivityAsync(string username, string activityId, IObjectOrLink activity, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Saving inbox activity {ActivityId} for {Username}", activityId, username);
        
        await SaveActivityAsync(username, activityId, activity, isInbox: true, isOutbox: false, cancellationToken);
    }

    public async Task SaveOutboxActivityAsync(string username, string activityId, IObjectOrLink activity, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Saving outbox activity {ActivityId} for {Username}", activityId, username);
        
        await SaveActivityAsync(username, activityId, activity, isInbox: false, isOutbox: true, cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetInboxActivitiesAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting inbox activities for {Username} (limit: {Limit}, offset: {Offset})", username, limit, offset);
        
        var entities = await _context.Activities
            .Where(a => a.Username == username && a.IsInbox)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(e => DeserializeActivity(e.ActivityJson)).Where(a => a != null).Cast<IObjectOrLink>();
    }

    public async Task<IEnumerable<IObjectOrLink>> GetOutboxActivitiesAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting outbox activities for {Username} (limit: {Limit}, offset: {Offset})", username, limit, offset);
        
        var entities = await _context.Activities
            .Where(a => a.Username == username && a.IsOutbox)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(e => DeserializeActivity(e.ActivityJson)).Where(a => a != null).Cast<IObjectOrLink>();
    }

    public async Task<IObjectOrLink?> GetActivityByIdAsync(string activityId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting activity by ID: {ActivityId}", activityId);
        
        var entity = await _context.Activities
            .FirstOrDefaultAsync(a => a.ActivityId == activityId, cancellationToken);

        if (entity == null)
            return null;

        return DeserializeActivity(entity.ActivityJson);
    }

    public async Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting activity: {ActivityId}", activityId);
        
        var entity = await _context.Activities
            .FirstOrDefaultAsync(a => a.ActivityId == activityId, cancellationToken);

        if (entity != null)
        {
            _context.Activities.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> GetInboxCountAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.Activities
            .CountAsync(a => a.Username == username && a.IsInbox, cancellationToken);
    }

    public async Task<int> GetOutboxCountAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.Activities
            .CountAsync(a => a.Username == username && a.IsOutbox, cancellationToken);
    }

    public async Task<bool> ActivityExistsAsync(string activityId, CancellationToken cancellationToken = default)
    {
        return await _context.Activities
            .AnyAsync(a => a.ActivityId == activityId, cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetRepliesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting replies for object {ObjectId} (limit: {Limit}, offset: {Offset})", objectId, limit, offset);
        
        // Use normalized InReplyTo field for efficient querying
        var entities = await _context.Activities
            .Where(a => a.InReplyTo == objectId)
            .OrderByDescending(a => a.Published ?? a.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(e => DeserializeActivity(e.ActivityJson)).Where(a => a != null).Cast<IObjectOrLink>();
    }

    public async Task<int> GetRepliesCountAsync(string objectId, CancellationToken cancellationToken = default)
    {
        // First check if we have a denormalized count
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.ActivityId == objectId, cancellationToken);
        if (activity != null && activity.ReplyCount > 0)
        {
            return activity.ReplyCount;
        }
        
        // Otherwise count actual replies
        return await _context.Activities
            .CountAsync(a => a.InReplyTo == objectId, cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetLikesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting likes for object {ObjectId} (limit: {Limit}, offset: {Offset})", objectId, limit, offset);
        
        // Use normalized fields for efficient querying
        var entities = await _context.Activities
            .Where(a => a.ActivityType == "Like" && a.ObjectId == objectId)
            .OrderByDescending(a => a.Published ?? a.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(e => DeserializeActivity(e.ActivityJson)).Where(a => a != null).Cast<IObjectOrLink>();
    }

    public async Task<int> GetLikesCountAsync(string objectId, CancellationToken cancellationToken = default)
    {
        // First check if we have a denormalized count
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.ActivityId == objectId, cancellationToken);
        if (activity != null && activity.LikeCount > 0)
        {
            return activity.LikeCount;
        }
        
        // Otherwise count actual likes
        return await _context.Activities
            .CountAsync(a => a.ActivityType == "Like" && a.ObjectId == objectId, cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetSharesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting shares for object {ObjectId} (limit: {Limit}, offset: {Offset})", objectId, limit, offset);
        
        // Use normalized fields for efficient querying
        var entities = await _context.Activities
            .Where(a => a.ActivityType == "Announce" && a.ObjectId == objectId)
            .OrderByDescending(a => a.Published ?? a.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(e => DeserializeActivity(e.ActivityJson)).Where(a => a != null).Cast<IObjectOrLink>();
    }

    public async Task<int> GetSharesCountAsync(string objectId, CancellationToken cancellationToken = default)
    {
        // First check if we have a denormalized count
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.ActivityId == objectId, cancellationToken);
        if (activity != null && activity.ShareCount > 0)
        {
            return activity.ShareCount;
        }
        
        // Otherwise count actual shares
        return await _context.Activities
            .CountAsync(a => a.ActivityType == "Announce" && a.ObjectId == objectId, cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetLikedByActorAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting liked objects for actor {Username} (limit: {Limit}, offset: {Offset})", username, limit, offset);
        
        // Get Like activities by this user and extract the liked objects
        var entities = await _context.Activities
            .Where(a => a.Username == username && a.ActivityType == "Like" && a.IsOutbox)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(e => DeserializeActivity(e.ActivityJson)).Where(a => a != null).Cast<IObjectOrLink>();
    }

    public async Task<int> GetLikedByActorCountAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.Activities
            .CountAsync(a => a.Username == username && a.ActivityType == "Like" && a.IsOutbox, cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetSharedByActorAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting shared objects for actor {Username} (limit: {Limit}, offset: {Offset})", username, limit, offset);
        
        // Get Announce activities by this user and extract the announced objects
        var entities = await _context.Activities
            .Where(a => a.Username == username && a.ActivityType == "Announce" && a.IsOutbox)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(e => DeserializeActivity(e.ActivityJson)).Where(a => a != null).Cast<IObjectOrLink>();
    }

    public async Task<int> GetSharedByActorCountAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.Activities
            .CountAsync(a => a.Username == username && a.ActivityType == "Announce" && a.IsOutbox, cancellationToken);
    }

    private async Task SaveActivityAsync(string username, string activityId, IObjectOrLink activity, bool isInbox, bool isOutbox, CancellationToken cancellationToken)
    {
        var json = SerializeActivity(activity);
        var activityType = ExtractActivityType(activity);

        var entity = await _context.Activities
            .FirstOrDefaultAsync(a => a.ActivityId == activityId, cancellationToken);

        var isNew = entity == null;
        if (isNew)
        {
            entity = new ActivityEntity
            {
                ActivityId = activityId,
                Username = username,
                ActivityType = activityType,
                ActivityJson = json,
                IsInbox = isInbox,
                IsOutbox = isOutbox,
                CreatedAt = DateTime.UtcNow
            };
            
            // Extract normalized fields from ActivityStreams
            _extractor.ExtractActivityFields(activity, entity);
            
            _context.Activities.Add(entity);
        }
        else
        {
            entity.ActivityJson = json;
            entity.ActivityType = activityType;
            entity.IsInbox = isInbox || entity.IsInbox;
            entity.IsOutbox = isOutbox || entity.IsOutbox;
            
            // Re-extract fields in case of update
            _extractor.ExtractActivityFields(activity, entity);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Extract and save related entities for new activities
        if (isNew && activity is IObject obj)
        {
            // Save recipients
            var recipients = _extractor.ExtractRecipients(obj, activityId: entity.Id);
            if (recipients.Any())
            {
                await _context.ActivityRecipients.AddRangeAsync(recipients, cancellationToken);
            }

            // Save attachments
            var attachments = _extractor.ExtractAttachments(obj, activityId: entity.Id);
            if (attachments.Any())
            {
                entity.AttachmentCount = attachments.Count;
                await _context.ActivityAttachments.AddRangeAsync(attachments, cancellationToken);
            }

            // Save tags
            var tags = _extractor.ExtractTags(obj, activityId: entity.Id);
            if (tags.Any())
            {
                await _context.ActivityTags.AddRangeAsync(tags, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Update counts based on activity type
            await UpdateCountsForActivityAsync(entity, cancellationToken);
        }
    }

    private static string SerializeActivity(IObjectOrLink activity)
    {
        // TODO: Implement proper ActivityStreams serialization
        return JsonSerializer.Serialize(activity);
    }

    private static IObjectOrLink? DeserializeActivity(string json)
    {
        // TODO: Implement proper ActivityStreams deserialization
        return JsonSerializer.Deserialize<IObjectOrLink>(json);
    }

    private static string ExtractActivityType(IObjectOrLink activity)
    {
        if (activity is IObject obj && obj.Type != null && obj.Type.Any())
        {
            return obj.Type.First();
        }
        return "Unknown";
    }

    /// <summary>
    /// Updates denormalized counts based on activity type
    /// </summary>
    private async Task UpdateCountsForActivityAsync(ActivityEntity entity, CancellationToken cancellationToken)
    {
        try
        {
            // Update counts based on activity type
            switch (entity.ActivityType)
            {
                case "Create":
                    // Increment status count for the actor
                    if (!string.IsNullOrEmpty(entity.Username))
                    {
                        await _countManager.IncrementStatusCountAsync(entity.Username, cancellationToken);
                    }
                    // If it's a reply, increment reply count on parent
                    if (!string.IsNullOrEmpty(entity.InReplyTo))
                    {
                        await _countManager.IncrementReplyCountAsync(entity.InReplyTo, cancellationToken);
                    }
                    break;

                case "Like":
                    // Increment like count on target object
                    if (!string.IsNullOrEmpty(entity.ObjectId))
                    {
                        await _countManager.IncrementLikeCountAsync(entity.ObjectId, cancellationToken);
                    }
                    break;

                case "Announce":
                    // Increment share count on target object
                    if (!string.IsNullOrEmpty(entity.ObjectId))
                    {
                        await _countManager.IncrementShareCountAsync(entity.ObjectId, cancellationToken);
                    }
                    break;

                case "Undo":
                    // Handle undo of Like or Announce
                    if (!string.IsNullOrEmpty(entity.ObjectId))
                    {
                        // Try to determine what was undone
                        var originalActivity = await _context.Activities
                            .FirstOrDefaultAsync(a => a.ActivityId == entity.ObjectId, cancellationToken);
                        
                        if (originalActivity != null)
                        {
                            if (originalActivity.ActivityType == "Like" && !string.IsNullOrEmpty(originalActivity.ObjectId))
                            {
                                await _countManager.DecrementLikeCountAsync(originalActivity.ObjectId, cancellationToken);
                            }
                            else if (originalActivity.ActivityType == "Announce" && !string.IsNullOrEmpty(originalActivity.ObjectId))
                            {
                                await _countManager.DecrementShareCountAsync(originalActivity.ObjectId, cancellationToken);
                            }
                        }
                    }
                    break;

                case "Delete":
                    // Decrement status count for the actor
                    if (!string.IsNullOrEmpty(entity.Username))
                    {
                        await _countManager.DecrementStatusCountAsync(entity.Username, cancellationToken);
                    }
                    // If deleting a reply, decrement reply count on parent
                    if (!string.IsNullOrEmpty(entity.InReplyTo))
                    {
                        await _countManager.DecrementReplyCountAsync(entity.InReplyTo, cancellationToken);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating counts for activity {ActivityId}", entity.ActivityId);
        }
    }
}
