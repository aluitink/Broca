using System.Text.Json;
using System.Text.Json.Serialization;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.MySql.Entities;
using KristofferStrube.ActivityStreams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CollectionType = Broca.ActivityPub.Persistence.MySql.Entities.CollectionType;

namespace Broca.ActivityPub.Persistence.MySql.Repositories;

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
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    public Task SaveInboxActivityAsync(string username, string activityId, IObjectOrLink activity, CancellationToken cancellationToken = default)
        => SaveActivityToActorCollectionAsync(username, activityId, activity, CollectionType.Inbox, cancellationToken);

    public Task SaveOutboxActivityAsync(string username, string activityId, IObjectOrLink activity, CancellationToken cancellationToken = default)
        => SaveActivityToActorCollectionAsync(username, activityId, activity, CollectionType.Outbox, cancellationToken);

    private async Task SaveActivityToActorCollectionAsync(string username, string activityUri, IObjectOrLink activity, CollectionType collectionType, CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var json = JsonSerializer.Serialize(activity, typeof(IObjectOrLink), _jsonOptions);
        var activityType = (activity as IObject)?.Type?.FirstOrDefault();

        var activityEntity = await db.Activities
            .FirstOrDefaultAsync(a => a.ActivityUri == activityUri, cancellationToken);

        if (activityEntity is null)
        {
            activityEntity = new ActivityEntity
            {
                ActivityUri = activityUri,
                ActivityType = activityType,
                ActivityJson = json,
                CreatedAt = DateTime.UtcNow,
            };
            db.Activities.Add(activityEntity);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            activityEntity.ActivityJson = json;
            activityEntity.ActivityType = activityType;
        }

        var collectionId = await GetOrCreateActorCollectionIdAsync(db, username, collectionType, cancellationToken);

        var memberExists = await db.CollectionMembers
            .AnyAsync(m => m.ActivityId == activityEntity.Id && m.CollectionId == collectionId, cancellationToken);

        if (!memberExists)
        {
            db.CollectionMembers.Add(new CollectionMemberEntity
            {
                ActivityId = activityEntity.Id,
                CollectionId = collectionId,
                LinkedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<IEnumerable<IObjectOrLink>> GetInboxActivitiesAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
        => GetActorCollectionActivitiesAsync(username, CollectionType.Inbox, limit, offset, cancellationToken);

    public Task<IEnumerable<IObjectOrLink>> GetOutboxActivitiesAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
        => GetActorCollectionActivitiesAsync(username, CollectionType.Outbox, limit, offset, cancellationToken);

    public async Task<IObjectOrLink?> GetActivityByIdAsync(string activityId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var json = await db.Activities
            .AsNoTracking()
            .Where(a => a.ActivityUri == activityId)
            .Select(a => a.ActivityJson)
            .FirstOrDefaultAsync(cancellationToken);
        return json is null ? null : DeserializeOne(json);
    }

    public async Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Activities.Where(a => a.ActivityUri == activityId).ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> GetInboxCountAsync(string username, CancellationToken cancellationToken = default)
        => GetActorCollectionCountAsync(username, CollectionType.Inbox, cancellationToken);

    public Task<int> GetOutboxCountAsync(string username, CancellationToken cancellationToken = default)
        => GetActorCollectionCountAsync(username, CollectionType.Outbox, cancellationToken);

    public Task<IEnumerable<IObjectOrLink>> GetRepliesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
        => GetTargetCollectionActivitiesAsync(CollectionType.Replies, objectId, limit, offset, cancellationToken);

    public Task<int> GetRepliesCountAsync(string objectId, CancellationToken cancellationToken = default)
        => GetTargetCollectionCountAsync(CollectionType.Replies, objectId, cancellationToken);

    public Task<IEnumerable<IObjectOrLink>> GetLikesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
        => GetTargetCollectionActivitiesAsync(CollectionType.Likes, objectId, limit, offset, cancellationToken);

    public Task<int> GetLikesCountAsync(string objectId, CancellationToken cancellationToken = default)
        => GetTargetCollectionCountAsync(CollectionType.Likes, objectId, cancellationToken);

    public Task<IEnumerable<IObjectOrLink>> GetSharesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
        => GetTargetCollectionActivitiesAsync(CollectionType.Shares, objectId, limit, offset, cancellationToken);

    public Task<int> GetSharesCountAsync(string objectId, CancellationToken cancellationToken = default)
        => GetTargetCollectionCountAsync(CollectionType.Shares, objectId, cancellationToken);

    public Task<IEnumerable<IObjectOrLink>> GetLikedByActorAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
        => GetOutboxByTypeAsync(username, "Like", limit, offset, cancellationToken);

    public Task<int> GetLikedByActorCountAsync(string username, CancellationToken cancellationToken = default)
        => GetOutboxByTypeCountAsync(username, "Like", cancellationToken);

    public Task<IEnumerable<IObjectOrLink>> GetSharedByActorAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
        => GetOutboxByTypeAsync(username, "Announce", limit, offset, cancellationToken);

    public Task<int> GetSharedByActorCountAsync(string username, CancellationToken cancellationToken = default)
        => GetOutboxByTypeCountAsync(username, "Announce", cancellationToken);

    public async Task MarkObjectAsDeletedAsync(string objectId, CancellationToken cancellationToken = default)
    {
        var tombstone = new Tombstone
        {
            Id = objectId,
            Type = new[] { "Tombstone" },
            Deleted = DateTime.UtcNow,
        };
        var json = JsonSerializer.Serialize<IObjectOrLink>(tombstone, _jsonOptions);

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Activities
            .Where(a => a.ActivityUri == objectId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ActivityJson, json), cancellationToken);
    }

    public async Task RecordInteractionAsync(string objectId, ActivityInteractionType type, string activityId, CancellationToken cancellationToken = default)
    {
        var collectionType = type switch
        {
            ActivityInteractionType.Like => CollectionType.Likes,
            ActivityInteractionType.Announce => CollectionType.Shares,
            ActivityInteractionType.Reply => CollectionType.Replies,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var activityEntity = await db.Activities
            .FirstOrDefaultAsync(a => a.ActivityUri == activityId, cancellationToken);
        if (activityEntity is null) return;

        var collectionId = await GetOrCreateTargetCollectionIdAsync(db, collectionType, objectId, cancellationToken);

        var exists = await db.CollectionMembers
            .AnyAsync(m => m.ActivityId == activityEntity.Id && m.CollectionId == collectionId, cancellationToken);

        if (!exists)
        {
            db.CollectionMembers.Add(new CollectionMemberEntity
            {
                ActivityId = activityEntity.Id,
                CollectionId = collectionId,
                LinkedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveInteractionAsync(string objectId, ActivityInteractionType type, string activityId, CancellationToken cancellationToken = default)
    {
        var collectionType = type switch
        {
            ActivityInteractionType.Like => CollectionType.Likes,
            ActivityInteractionType.Announce => CollectionType.Shares,
            ActivityInteractionType.Reply => CollectionType.Replies,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var query = from m in db.CollectionMembers
                    join c in db.Collections on m.CollectionId equals c.Id
                    join a in db.Activities on m.ActivityId equals a.Id
                    where c.Type == collectionType && c.TargetUri == objectId && a.ActivityUri == activityId
                    select m;

        await query.ExecuteDeleteAsync(cancellationToken);
    }

    // IActivityStatistics

    public async Task<int> CountCreateActivitiesSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.CollectionMembers
            .Where(m => m.Collection.Type == CollectionType.Outbox
                && m.Activity.ActivityType == "Create"
                && m.Activity.CreatedAt >= since)
            .CountAsync(cancellationToken);
    }

    public async Task<int> CountActiveActorsSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.CollectionMembers
            .Where(m => m.Collection.Type == CollectionType.Outbox
                && m.Activity.ActivityType == "Create"
                && m.Activity.CreatedAt >= since)
            .Select(m => m.Collection.ActorId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    // ISearchableActivityRepository

    public async Task<IEnumerable<IObjectOrLink>> GetInboxActivitiesAsync(string username, CollectionSearchParameters search, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await ApplySearch(ActorCollectionActivityQuery(db, username, CollectionType.Inbox), search)
            .Skip(offset).Take(limit).Select(a => a.ActivityJson).ToListAsync(cancellationToken);
        return Deserialize(rows);
    }

    public async Task<int> GetInboxCountAsync(string username, CollectionSearchParameters search, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await ApplySearch(ActorCollectionActivityQuery(db, username, CollectionType.Inbox), search).CountAsync(cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetOutboxActivitiesAsync(string username, CollectionSearchParameters search, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await ApplySearch(ActorCollectionActivityQuery(db, username, CollectionType.Outbox), search)
            .Skip(offset).Take(limit).Select(a => a.ActivityJson).ToListAsync(cancellationToken);
        return Deserialize(rows);
    }

    public async Task<int> GetOutboxCountAsync(string username, CollectionSearchParameters search, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await ApplySearch(ActorCollectionActivityQuery(db, username, CollectionType.Outbox), search).CountAsync(cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetRepliesAsync(string objectId, CollectionSearchParameters search, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await ApplySearch(TargetCollectionActivityQuery(db, CollectionType.Replies, objectId), search)
            .Skip(offset).Take(limit).Select(a => a.ActivityJson).ToListAsync(cancellationToken);
        return Deserialize(rows);
    }

    public async Task<int> GetRepliesCountAsync(string objectId, CollectionSearchParameters search, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await ApplySearch(TargetCollectionActivityQuery(db, CollectionType.Replies, objectId), search).CountAsync(cancellationToken);
    }

    // Helpers

    private async Task<long> GetOrCreateActorCollectionIdAsync(BrocaDbContext db, string username, CollectionType type, CancellationToken cancellationToken)
    {
        var key = username.ToLowerInvariant();
        var collection = await db.Collections
            .FirstOrDefaultAsync(c => c.Actor.Username == key && c.Type == type && c.Name == null, cancellationToken);

        if (collection is not null)
            return collection.Id;

        var actor = await db.Actors.FirstAsync(a => a.Username == key, cancellationToken);
        collection = new CollectionEntity
        {
            ActorId = actor.Id,
            Type = type,
        };
        db.Collections.Add(collection);
        await db.SaveChangesAsync(cancellationToken);
        return collection.Id;
    }

    private async Task<long> GetOrCreateTargetCollectionIdAsync(BrocaDbContext db, CollectionType type, string targetUri, CancellationToken cancellationToken)
    {
        var collection = await db.Collections
            .FirstOrDefaultAsync(c => c.Type == type && c.TargetUri == targetUri, cancellationToken);

        if (collection is not null)
            return collection.Id;

        var systemActor = await db.Actors.FirstAsync(a => a.Username == "sys", cancellationToken);
        collection = new CollectionEntity
        {
            ActorId = systemActor.Id,
            Type = type,
            TargetUri = targetUri,
        };
        db.Collections.Add(collection);
        await db.SaveChangesAsync(cancellationToken);
        return collection.Id;
    }

    private async Task<IEnumerable<IObjectOrLink>> GetActorCollectionActivitiesAsync(string username, CollectionType type, int limit, int offset, CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var rows = await db.CollectionMembers
            .AsNoTracking()
            .Where(m => m.Collection.Actor.Username == key && m.Collection.Type == type && m.Collection.Name == null)
            .OrderByDescending(m => m.Activity.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .Select(m => m.Activity.ActivityJson)
            .ToListAsync(cancellationToken);
        return Deserialize(rows);
    }

    private async Task<int> GetActorCollectionCountAsync(string username, CollectionType type, CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        return await db.CollectionMembers
            .CountAsync(m => m.Collection.Actor.Username == key && m.Collection.Type == type && m.Collection.Name == null, cancellationToken);
    }

    private async Task<IEnumerable<IObjectOrLink>> GetTargetCollectionActivitiesAsync(CollectionType type, string targetUri, int limit, int offset, CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.CollectionMembers
            .AsNoTracking()
            .Where(m => m.Collection.Type == type && m.Collection.TargetUri == targetUri)
            .OrderByDescending(m => m.Activity.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .Select(m => m.Activity.ActivityJson)
            .ToListAsync(cancellationToken);
        return Deserialize(rows);
    }

    private async Task<int> GetTargetCollectionCountAsync(CollectionType type, string targetUri, CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.CollectionMembers
            .CountAsync(m => m.Collection.Type == type && m.Collection.TargetUri == targetUri, cancellationToken);
    }

    private async Task<IEnumerable<IObjectOrLink>> GetOutboxByTypeAsync(string username, string activityType, int limit, int offset, CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var rows = await db.CollectionMembers
            .AsNoTracking()
            .Where(m => m.Collection.Actor.Username == key
                && m.Collection.Type == CollectionType.Outbox
                && m.Collection.Name == null
                && m.Activity.ActivityType == activityType)
            .OrderByDescending(m => m.Activity.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .Select(m => m.Activity.ActivityJson)
            .ToListAsync(cancellationToken);
        return Deserialize(rows);
    }

    private async Task<int> GetOutboxByTypeCountAsync(string username, string activityType, CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        return await db.CollectionMembers
            .CountAsync(m => m.Collection.Actor.Username == key
                && m.Collection.Type == CollectionType.Outbox
                && m.Collection.Name == null
                && m.Activity.ActivityType == activityType, cancellationToken);
    }

    private IQueryable<ActivityEntity> ActorCollectionActivityQuery(BrocaDbContext db, string username, CollectionType type)
    {
        var key = username.ToLowerInvariant();
        return db.CollectionMembers
            .AsNoTracking()
            .Where(m => m.Collection.Actor.Username == key && m.Collection.Type == type && m.Collection.Name == null)
            .Select(m => m.Activity);
    }

    private IQueryable<ActivityEntity> TargetCollectionActivityQuery(BrocaDbContext db, CollectionType type, string targetUri)
        => db.CollectionMembers
            .AsNoTracking()
            .Where(m => m.Collection.Type == type && m.Collection.TargetUri == targetUri)
            .Select(m => m.Activity);

    private IQueryable<ActivityEntity> ApplySearch(IQueryable<ActivityEntity> query, CollectionSearchParameters search)
    {
        if (!string.IsNullOrWhiteSpace(search.Search))
            query = query.Where(a => a.ActivityJson.Contains(search.Search));

        if (!string.IsNullOrWhiteSpace(search.Filter))
            query = query.Where(a => a.ActivityType == search.Filter);

        return search.OrderBy?.ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(a => a.CreatedAt),
            _ => query.OrderByDescending(a => a.CreatedAt),
        };
    }

    private IEnumerable<IObjectOrLink> Deserialize(IEnumerable<string> jsonList)
    {
        var results = new List<IObjectOrLink>();
        foreach (var json in jsonList)
        {
            var item = DeserializeOne(json);
            if (item is not null) results.Add(item);
        }
        return results;
    }

    private IObjectOrLink? DeserializeOne(string json)
    {
        try { return JsonSerializer.Deserialize<IObjectOrLink>(json, _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to deserialize activity JSON"); return null; }
    }
}
