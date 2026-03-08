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

public class MySqlActorRepository : IActorRepository, IActorStatistics
{
    private readonly IDbContextFactory<BrocaDbContext> _contextFactory;
    private readonly ILogger<MySqlActorRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public MySqlActorRepository(
        IDbContextFactory<BrocaDbContext> contextFactory,
        ILogger<MySqlActorRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    public async Task<Actor?> GetActorByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Actors
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Username == username.ToLowerInvariant(), cancellationToken);
        return Deserialize(entity?.ActorJson);
    }

    public async Task<Actor?> GetActorByIdAsync(string actorId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Actors
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ActorUri == actorId, cancellationToken);
        return Deserialize(entity?.ActorJson);
    }

    public async Task SaveActorAsync(string username, Actor actor, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var existing = await db.Actors.FirstOrDefaultAsync(a => a.Username == key, cancellationToken);
        var json = JsonSerializer.Serialize<IObjectOrLink>(actor, _jsonOptions);

        if (existing is null)
        {
            db.Actors.Add(new ActorEntity
            {
                Username = key,
                ActorUri = actor.Id,
                ActorJson = json,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.ActorUri = actor.Id;
            existing.ActorJson = json;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteActorAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        await db.Actors.Where(a => a.Username == key).ExecuteDeleteAsync(cancellationToken);
    }

    // Followers

    public async Task<IEnumerable<string>> GetFollowersAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await RelationshipQuery(db, username, ActorRelationshipFlags.Follower)
            .Select(r => r.TargetActorUri)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetFollowersAsync(string username, int limit, int offset, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await RelationshipQuery(db, username, ActorRelationshipFlags.Follower)
            .Select(r => r.TargetActorUri)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetFollowersCountAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await RelationshipQuery(db, username, ActorRelationshipFlags.Follower)
            .CountAsync(cancellationToken);
    }

    public Task AddFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
        => SetFlagAsync(username, followerActorId, ActorRelationshipFlags.Follower, ActorRelationshipFlags.PendingFollower, cancellationToken);

    public Task RemoveFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
        => ClearFlagAsync(username, followerActorId, ActorRelationshipFlags.Follower, cancellationToken);

    // Following

    public async Task<IEnumerable<string>> GetFollowingAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await RelationshipQuery(db, username, ActorRelationshipFlags.Following)
            .Select(r => r.TargetActorUri)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetFollowingAsync(string username, int limit, int offset, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await RelationshipQuery(db, username, ActorRelationshipFlags.Following)
            .Select(r => r.TargetActorUri)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetFollowingCountAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await RelationshipQuery(db, username, ActorRelationshipFlags.Following)
            .CountAsync(cancellationToken);
    }

    public Task AddFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
        => SetFlagAsync(username, followingActorId, ActorRelationshipFlags.Following, ActorRelationshipFlags.PendingFollowing, cancellationToken);

    public Task RemoveFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
        => ClearFlagAsync(username, followingActorId, ActorRelationshipFlags.Following, cancellationToken);

    // Pending followers

    public async Task<IEnumerable<string>> GetPendingFollowersAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await RelationshipQuery(db, username, ActorRelationshipFlags.PendingFollower)
            .Select(r => r.TargetActorUri)
            .ToListAsync(cancellationToken);
    }

    public Task AddPendingFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
        => SetFlagAsync(username, followerActorId, ActorRelationshipFlags.PendingFollower, ActorRelationshipFlags.None, cancellationToken);

    public Task RemovePendingFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
        => ClearFlagAsync(username, followerActorId, ActorRelationshipFlags.PendingFollower, cancellationToken);

    // Pending following

    public async Task<IEnumerable<string>> GetPendingFollowingAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await RelationshipQuery(db, username, ActorRelationshipFlags.PendingFollowing)
            .Select(r => r.TargetActorUri)
            .ToListAsync(cancellationToken);
    }

    public Task AddPendingFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
        => SetFlagAsync(username, followingActorId, ActorRelationshipFlags.PendingFollowing, ActorRelationshipFlags.None, cancellationToken);

    public Task RemovePendingFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
        => ClearFlagAsync(username, followingActorId, ActorRelationshipFlags.PendingFollowing, cancellationToken);

    // Custom collections

    public async Task<IEnumerable<CustomCollectionDefinition>> GetCollectionDefinitionsAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var entities = await db.Collections
            .AsNoTracking()
            .Where(c => c.Actor.Username == key && c.Type == CollectionType.Custom)
            .ToListAsync(cancellationToken);
        return entities.Select(e => DeserializeCollection(e.DefinitionJson)!).Where(d => d != null);
    }

    public async Task<CustomCollectionDefinition?> GetCollectionDefinitionAsync(string username, string collectionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var entity = await db.Collections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Actor.Username == key && c.Type == CollectionType.Custom && c.Name == collectionId, cancellationToken);
        return entity is null ? null : DeserializeCollection(entity.DefinitionJson);
    }

    public async Task SaveCollectionDefinitionAsync(string username, CustomCollectionDefinition definition, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var existing = await db.Collections
            .FirstOrDefaultAsync(c => c.Actor.Username == key && c.Type == CollectionType.Custom && c.Name == definition.Id, cancellationToken);
        var json = JsonSerializer.Serialize(definition, _jsonOptions);

        if (existing is null)
        {
            var actor = await db.Actors.FirstAsync(a => a.Username == key, cancellationToken);
            db.Collections.Add(new CollectionEntity
            {
                ActorId = actor.Id,
                Type = CollectionType.Custom,
                Name = definition.Id,
                DefinitionJson = json,
            });
        }
        else
        {
            existing.DefinitionJson = json;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCollectionDefinitionAsync(string username, string collectionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        await db.Collections
            .Where(c => c.Actor.Username == key && c.Type == CollectionType.Custom && c.Name == collectionId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task AddToCollectionAsync(string username, string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var collection = await db.Collections
            .FirstOrDefaultAsync(c => c.Actor.Username == key && c.Type == CollectionType.Custom && c.Name == collectionId, cancellationToken);
        if (collection is null) return;

        var activity = await db.Activities.FirstOrDefaultAsync(a => a.ActivityUri == itemId, cancellationToken);
        if (activity is null) return;

        var exists = await db.CollectionMembers
            .AnyAsync(m => m.CollectionId == collection.Id && m.ActivityId == activity.Id, cancellationToken);

        if (!exists)
        {
            db.CollectionMembers.Add(new CollectionMemberEntity
            {
                CollectionId = collection.Id,
                ActivityId = activity.Id,
                LinkedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveFromCollectionAsync(string username, string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();

        var query = from m in db.CollectionMembers
                    join c in db.Collections on m.CollectionId equals c.Id
                    join a in db.Activities on m.ActivityId equals a.Id
                    where c.Actor.Username == key && c.Type == CollectionType.Custom && c.Name == collectionId && a.ActivityUri == itemId
                    select m;

        await query.ExecuteDeleteAsync(cancellationToken);
    }

    // Local usernames

    public async Task<IEnumerable<string>> GetAllLocalUsernamesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Actors
            .AsNoTracking()
            .Select(a => a.Username)
            .ToListAsync(cancellationToken);
    }

    // IActorStatistics

    public async Task<int> CountLocalActorsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Actors.CountAsync(cancellationToken);
    }

    // Helpers

    private IQueryable<ActorRelationshipEntity> RelationshipQuery(BrocaDbContext db, string username, ActorRelationshipFlags flag)
    {
        var key = username.ToLowerInvariant();
        return db.ActorRelationships
            .AsNoTracking()
            .Where(r => r.Actor.Username == key && (r.Flags & flag) != 0);
    }

    private async Task SetFlagAsync(
        string username,
        string targetActorUri,
        ActorRelationshipFlags flagToSet,
        ActorRelationshipFlags flagToClear,
        CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var actor = await db.Actors.FirstAsync(a => a.Username == key, cancellationToken);
        var rel = await db.ActorRelationships
            .FirstOrDefaultAsync(r => r.ActorId == actor.Id && r.TargetActorUri == targetActorUri, cancellationToken);

        if (rel is null)
        {
            db.ActorRelationships.Add(new ActorRelationshipEntity
            {
                ActorId = actor.Id,
                TargetActorUri = targetActorUri,
                Flags = flagToSet,
            });
        }
        else
        {
            rel.Flags = (rel.Flags | flagToSet) & ~flagToClear;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ClearFlagAsync(
        string username,
        string targetActorUri,
        ActorRelationshipFlags flagToClear,
        CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var actor = await db.Actors.FirstAsync(a => a.Username == key, cancellationToken);
        var rel = await db.ActorRelationships
            .FirstOrDefaultAsync(r => r.ActorId == actor.Id && r.TargetActorUri == targetActorUri, cancellationToken);
        if (rel is null) return;

        rel.Flags &= ~flagToClear;
        if (rel.Flags == ActorRelationshipFlags.None)
            db.ActorRelationships.Remove(rel);

        await db.SaveChangesAsync(cancellationToken);
    }

    private Actor? Deserialize(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<Actor>(json, _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to deserialize actor JSON"); return null; }
    }

    private CustomCollectionDefinition? DeserializeCollection(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<CustomCollectionDefinition>(json, _jsonOptions); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to deserialize collection definition JSON"); return null; }
    }
}
