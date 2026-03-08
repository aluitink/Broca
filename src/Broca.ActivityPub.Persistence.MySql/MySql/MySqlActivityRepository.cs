using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.MySql.Entities;
using KristofferStrube.ActivityStreams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Persistence.MySql.MySql;

public class MySqlActivityRepository : IActivityRepository, IActivityStatistics, ISearchableActivityRepository
{
    private readonly IDbContextFactory<BrocaDbContext> _contextFactory;
    private readonly ILogger<MySqlActivityRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public MySqlActivityRepository(
        IDbContextFactory<BrocaDbContext> contextFactory,
        ILogger<MySqlActivityRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task SaveInboxActivityAsync(string username, string activityId, IObjectOrLink activity, CancellationToken cancellationToken = default)
        => await SaveActivityAsync(username, activityId, activity, "inbox", cancellationToken);

    public async Task SaveOutboxActivityAsync(string username, string activityId, IObjectOrLink activity, CancellationToken cancellationToken = default)
        => await SaveActivityAsync(username, activityId, activity, "outbox", cancellationToken);

    public async Task<IEnumerable<IObjectOrLink>> GetInboxActivitiesAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
        => await GetActivitiesAsync(username, "inbox", limit, offset, cancellationToken);

    public async Task<IEnumerable<IObjectOrLink>> GetOutboxActivitiesAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
        => await GetActivitiesAsync(username, "outbox", limit, offset, cancellationToken, activitiesOnly: true);

    public async Task<IObjectOrLink?> GetActivityByIdAsync(string activityId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Activities.FirstOrDefaultAsync(a => a.ActivityId == activityId, cancellationToken);
        return entity is null ? null : DeserializeActivity(entity.ActivityJson);
    }

    public async Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Activities.Where(a => a.ActivityId == activityId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> GetInboxCountAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Activities.CountAsync(
            a => a.Username == username.ToLowerInvariant() && a.Box == "inbox",
            cancellationToken);
    }

    public async Task<int> GetOutboxCountAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Activities.CountAsync(
            a => a.Username == username.ToLowerInvariant() && a.Box == "outbox",
            cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetRepliesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.Activities
            .Where(a => a.InReplyTo == objectId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset).Take(limit)
            .ToListAsync(cancellationToken);
        return DeserializeActivities(entities);
    }

    public async Task<int> GetRepliesCountAsync(string objectId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Activities.CountAsync(a => a.InReplyTo == objectId, cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetLikesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.Activities
            .Where(a => a.ActivityType == "Like" && a.ObjectId == objectId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset).Take(limit)
            .ToListAsync(cancellationToken);
        return DeserializeActivities(entities);
    }

    public async Task<int> GetLikesCountAsync(string objectId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Activities.CountAsync(
            a => a.ActivityType == "Like" && a.ObjectId == objectId,
            cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetSharesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.Activities
            .Where(a => a.ActivityType == "Announce" && a.ObjectId == objectId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset).Take(limit)
            .ToListAsync(cancellationToken);
        return DeserializeActivities(entities);
    }

    public async Task<int> GetSharesCountAsync(string objectId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Activities.CountAsync(
            a => a.ActivityType == "Announce" && a.ObjectId == objectId,
            cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetLikedByActorAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.Activities
            .Where(a => a.Username == username.ToLowerInvariant() && a.ActivityType == "Like")
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset).Take(limit)
            .ToListAsync(cancellationToken);
        return DeserializeActivities(entities);
    }

    public async Task<int> GetLikedByActorCountAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Activities.CountAsync(
            a => a.Username == username.ToLowerInvariant() && a.ActivityType == "Like",
            cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetSharedByActorAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.Activities
            .Where(a => a.Username == username.ToLowerInvariant() && a.ActivityType == "Announce")
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset).Take(limit)
            .ToListAsync(cancellationToken);
        return DeserializeActivities(entities);
    }

    public async Task<int> GetSharedByActorCountAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Activities.CountAsync(
            a => a.Username == username.ToLowerInvariant() && a.ActivityType == "Announce",
            cancellationToken);
    }

    public async Task MarkObjectAsDeletedAsync(string objectId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Activities.FirstOrDefaultAsync(a => a.ActivityId == objectId, cancellationToken);
        if (entity is not null)
        {
            var tombstone = new Tombstone { Id = objectId, FormerType = new List<string> { "Note" } };
            entity.ActivityJson = JsonSerializer.Serialize(tombstone, _jsonOptions);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> CountCreateActivitiesSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Activities.CountAsync(
            a => a.ActivityType == "Create" && a.CreatedAt >= since,
            cancellationToken);
    }

    public async Task<int> CountActiveActorsSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Activities
            .Where(a => a.ActivityType == "Create" && a.CreatedAt >= since && a.Box == "outbox")
            .Select(a => a.Username)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    // ISearchableActivityRepository

    public async Task<IEnumerable<IObjectOrLink>> GetInboxActivitiesAsync(
        string username,
        CollectionSearchParameters search,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (!search.HasSearchCriteria)
            return await GetInboxActivitiesAsync(username, limit, offset, cancellationToken);

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = BuildSearchQuery(db, username, "inbox", search);
        var entities = await query.Skip(offset).Take(limit).ToListAsync(cancellationToken);
        return DeserializeActivities(entities);
    }

    public async Task<int> GetInboxCountAsync(
        string username,
        CollectionSearchParameters search,
        CancellationToken cancellationToken = default)
    {
        if (!search.HasSearchCriteria)
            return await GetInboxCountAsync(username, cancellationToken);

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildSearchQuery(db, username, "inbox", search).CountAsync(cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetOutboxActivitiesAsync(
        string username,
        CollectionSearchParameters search,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (!search.HasSearchCriteria)
            return await GetOutboxActivitiesAsync(username, limit, offset, cancellationToken);

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = BuildSearchQuery(db, username, "outbox", search);
        var entities = await query.Skip(offset).Take(limit).ToListAsync(cancellationToken);
        return DeserializeActivities(entities);
    }

    public async Task<int> GetOutboxCountAsync(
        string username,
        CollectionSearchParameters search,
        CancellationToken cancellationToken = default)
    {
        if (!search.HasSearchCriteria)
            return await GetOutboxCountAsync(username, cancellationToken);

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildSearchQuery(db, username, "outbox", search).CountAsync(cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetRepliesAsync(
        string objectId,
        CollectionSearchParameters search,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (!search.HasSearchCriteria)
            return await GetRepliesAsync(objectId, limit, offset, cancellationToken);

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Activities.Where(a => a.InReplyTo == objectId);
        query = ApplySearch(query, search);
        var entities = await query.Skip(offset).Take(limit).ToListAsync(cancellationToken);
        return DeserializeActivities(entities);
    }

    public async Task<int> GetRepliesCountAsync(
        string objectId,
        CollectionSearchParameters search,
        CancellationToken cancellationToken = default)
    {
        if (!search.HasSearchCriteria)
            return await GetRepliesCountAsync(objectId, cancellationToken);

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Activities.Where(a => a.InReplyTo == objectId);
        query = ApplySearch(query, search);
        return await query.CountAsync(cancellationToken);
    }

    private async Task SaveActivityAsync(string username, string activityId, IObjectOrLink activity, string box, CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var json = JsonSerializer.Serialize(activity, typeof(IObjectOrLink), _jsonOptions);
        var (activityType, objectId, inReplyTo) = ExtractMetadata(activity);

        var existing = await db.Activities.FirstOrDefaultAsync(a => a.ActivityId == activityId, cancellationToken);
        if (existing is null)
        {
            db.Activities.Add(new ActivityEntity
            {
                ActivityId = activityId,
                Username = username.ToLowerInvariant(),
                Box = box,
                ActivityJson = json,
                ActivityType = activityType,
                ObjectId = objectId,
                InReplyTo = inReplyTo,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.ActivityJson = json;
            existing.ActivityType = activityType;
            existing.ObjectId = objectId;
            existing.InReplyTo = inReplyTo;
        }
        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Saved {Box} activity {ActivityId} for {Username}", box, activityId, username);
    }

    private async Task<IEnumerable<IObjectOrLink>> GetActivitiesAsync(
        string username, string box, int limit, int offset, CancellationToken cancellationToken,
        bool activitiesOnly = false)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Activities
            .Where(a => a.Username == username.ToLowerInvariant() && a.Box == box);
        if (activitiesOnly)
            query = query.Where(a => a.ActivityType != null);
        var entities = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset).Take(limit)
            .ToListAsync(cancellationToken);
        return DeserializeActivities(entities);
    }

    private IQueryable<ActivityEntity> BuildSearchQuery(BrocaDbContext db, string username, string box, CollectionSearchParameters search)
    {
        var query = db.Activities.Where(a => a.Username == username.ToLowerInvariant() && a.Box == box);
        return ApplySearch(query, search);
    }

    private static IQueryable<ActivityEntity> ApplySearch(IQueryable<ActivityEntity> query, CollectionSearchParameters search)
    {
        if (!string.IsNullOrWhiteSpace(search.Search))
            query = query.Where(a => a.ActivityJson.Contains(search.Search));

        if (!string.IsNullOrWhiteSpace(search.OrderBy) &&
            search.OrderBy.Equals("createdAt", StringComparison.OrdinalIgnoreCase))
            query = query.OrderBy(a => a.CreatedAt);
        else
            query = query.OrderByDescending(a => a.CreatedAt);

        return query;
    }

    private IObjectOrLink? DeserializeActivity(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<IObjectOrLink>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize activity JSON");
            return null;
        }
    }

    private IEnumerable<IObjectOrLink> DeserializeActivities(IEnumerable<ActivityEntity> entities)
    {
        var result = new List<IObjectOrLink>();
        foreach (var entity in entities)
        {
            var activity = DeserializeActivity(entity.ActivityJson);
            if (activity is not null)
                result.Add(activity);
        }
        return result;
    }

    private static (string? activityType, string? objectId, string? inReplyTo) ExtractMetadata(IObjectOrLink activity)
    {
        if (activity is not IObject obj)
            return (null, null, null);

        var activityType = obj.Type?.FirstOrDefault();
        string? objectId = null;
        string? inReplyTo = null;

        var objectProp = obj.GetType().GetProperty("Object");
        if (objectProp?.GetValue(obj) is IEnumerable<IObjectOrLink?> objects)
        {
            var first = objects.FirstOrDefault();
            objectId = first switch
            {
                IObject o when !string.IsNullOrEmpty(o.Id) => o.Id,
                ILink l when l.Href != null => l.Href.ToString(),
                _ => null
            };
        }

        var inReplyToProp = obj.GetType().GetProperty("InReplyTo");
        if (inReplyToProp?.GetValue(obj) is IEnumerable<IObjectOrLink?> replies)
        {
            var first = replies.FirstOrDefault();
            inReplyTo = first switch
            {
                IObject o when !string.IsNullOrEmpty(o.Id) => o.Id,
                ILink l when l.Href != null => l.Href.ToString(),
                _ => null
            };
        }

        return (activityType, objectId, inReplyTo);
    }
}
