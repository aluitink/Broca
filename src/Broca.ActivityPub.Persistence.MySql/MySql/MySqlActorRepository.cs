using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.MySql.Entities;
using KristofferStrube.ActivityStreams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Persistence.MySql.MySql;

public class MySqlActorRepository : IActorRepository, IActorStatistics
{
    private readonly IDbContextFactory<BrocaDbContext> _contextFactory;
    private readonly ILogger<MySqlActorRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ActivityPubServerOptions _serverOptions;

    public MySqlActorRepository(
        IDbContextFactory<BrocaDbContext> contextFactory,
        IOptions<ActivityPubServerOptions> serverOptions,
        ILogger<MySqlActorRepository> logger)
    {
        _contextFactory = contextFactory;
        _serverOptions = serverOptions.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<Actor?> GetActorByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Actors.FindAsync([username.ToLowerInvariant()], cancellationToken);
        return entity is null ? null : JsonSerializer.Deserialize<Actor>(entity.ActorJson, _jsonOptions);
    }

    public async Task<Actor?> GetActorByIdAsync(string actorId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Actors.FirstOrDefaultAsync(a => a.ActorId == actorId, cancellationToken);
        return entity is null ? null : JsonSerializer.Deserialize<Actor>(entity.ActorJson, _jsonOptions);
    }

    public async Task SaveActorAsync(string username, Actor actor, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var json = JsonSerializer.Serialize(actor, _jsonOptions);
        var isLocal = IsLocalActor(actor.Id);
        var domain = ExtractDomain(actor.Id);
        var existing = await db.Actors.FindAsync([key], cancellationToken);
        if (existing is null)
        {
            db.Actors.Add(new ActorEntity
            {
                Username = key,
                ActorId = actor.Id,
                IsLocal = isLocal,
                Domain = domain,
                ActorJson = json,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.ActorId = actor.Id;
            existing.IsLocal = isLocal;
            existing.Domain = domain;
            existing.ActorJson = json;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Saved actor {Username}", username);
    }

    public async Task DeleteActorAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        await db.Actors.Where(a => a.Username == key).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetFollowersAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Follows
            .Where(f => f.Username == username.ToLowerInvariant() && f.FollowType == FollowType.Follower)
            .Select(f => f.ActorId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetFollowersAsync(string username, int limit, int offset, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Follows
            .Where(f => f.Username == username.ToLowerInvariant() && f.FollowType == FollowType.Follower)
            .Select(f => f.ActorId)
            .Skip(offset).Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetFollowersCountAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Follows.CountAsync(
            f => f.Username == username.ToLowerInvariant() && f.FollowType == FollowType.Follower,
            cancellationToken);
    }

    public async Task<IEnumerable<string>> GetFollowingAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Follows
            .Where(f => f.Username == username.ToLowerInvariant() && f.FollowType == FollowType.Following)
            .Select(f => f.ActorId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetFollowingAsync(string username, int limit, int offset, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Follows
            .Where(f => f.Username == username.ToLowerInvariant() && f.FollowType == FollowType.Following)
            .Select(f => f.ActorId)
            .Skip(offset).Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetFollowingCountAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Follows.CountAsync(
            f => f.Username == username.ToLowerInvariant() && f.FollowType == FollowType.Following,
            cancellationToken);
    }

    public async Task AddFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
        => await AddFollowAsync(username, followerActorId, FollowType.Follower, cancellationToken);

    public async Task RemoveFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
        => await RemoveFollowAsync(username, followerActorId, FollowType.Follower, cancellationToken);

    public async Task AddFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
        => await AddFollowAsync(username, followingActorId, FollowType.Following, cancellationToken);

    public async Task RemoveFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
        => await RemoveFollowAsync(username, followingActorId, FollowType.Following, cancellationToken);

    public async Task<IEnumerable<string>> GetPendingFollowersAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Follows
            .Where(f => f.Username == username.ToLowerInvariant() && f.FollowType == FollowType.PendingFollower)
            .Select(f => f.ActorId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddPendingFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
        => await AddFollowAsync(username, followerActorId, FollowType.PendingFollower, cancellationToken);

    public async Task RemovePendingFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
        => await RemoveFollowAsync(username, followerActorId, FollowType.PendingFollower, cancellationToken);

    public async Task<IEnumerable<string>> GetPendingFollowingAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Follows
            .Where(f => f.Username == username.ToLowerInvariant() && f.FollowType == FollowType.PendingFollowing)
            .Select(f => f.ActorId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddPendingFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
        => await AddFollowAsync(username, followingActorId, FollowType.PendingFollowing, cancellationToken);

    public async Task RemovePendingFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
        => await RemoveFollowAsync(username, followingActorId, FollowType.PendingFollowing, cancellationToken);

    public async Task<IEnumerable<string>> GetAllLocalUsernamesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Actors.Where(a => a.IsLocal).Select(a => a.Username).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CustomCollectionDefinition>> GetCollectionDefinitionsAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.CollectionDefinitions
            .Where(c => c.Username == username.ToLowerInvariant())
            .ToListAsync(cancellationToken);
        return entities
            .Select(e => JsonSerializer.Deserialize<CustomCollectionDefinition>(e.DefinitionJson, _jsonOptions)!)
            .Where(d => d is not null)
            .ToList();
    }

    public async Task<CustomCollectionDefinition?> GetCollectionDefinitionAsync(string username, string collectionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.CollectionDefinitions.FindAsync(
            [username.ToLowerInvariant(), collectionId], cancellationToken);
        return entity is null ? null : JsonSerializer.Deserialize<CustomCollectionDefinition>(entity.DefinitionJson, _jsonOptions);
    }

    public async Task SaveCollectionDefinitionAsync(string username, CustomCollectionDefinition definition, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var json = JsonSerializer.Serialize(definition, _jsonOptions);
        var existing = await db.CollectionDefinitions.FindAsync([key, definition.Id], cancellationToken);
        if (existing is null)
            db.CollectionDefinitions.Add(new CollectionDefinitionEntity { Username = key, CollectionId = definition.Id, DefinitionJson = json });
        else
            existing.DefinitionJson = json;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCollectionDefinitionAsync(string username, string collectionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        await db.CollectionDefinitions
            .Where(c => c.Username == key && c.CollectionId == collectionId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task AddToCollectionAsync(string username, string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();

        var definition = await db.CollectionDefinitions.FindAsync([key, collectionId], cancellationToken);
        if (definition is not null)
        {
            var parsed = JsonSerializer.Deserialize<CustomCollectionDefinition>(definition.DefinitionJson, _jsonOptions);
            if (parsed?.Type == CollectionType.Query)
                throw new InvalidOperationException($"Cannot manually add items to query collection {collectionId}");
        }

        var exists = await db.CollectionItems.AnyAsync(
            c => c.Username == key && c.CollectionId == collectionId && c.ItemId == itemId,
            cancellationToken);
        if (!exists)
        {
            db.CollectionItems.Add(new CollectionItemEntity { Username = key, CollectionId = collectionId, ItemId = itemId });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveFromCollectionAsync(string username, string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();

        var definition = await db.CollectionDefinitions.FindAsync([key, collectionId], cancellationToken);
        if (definition is not null)
        {
            var parsed = JsonSerializer.Deserialize<CustomCollectionDefinition>(definition.DefinitionJson, _jsonOptions);
            if (parsed?.Type == CollectionType.Query)
                throw new InvalidOperationException($"Cannot manually remove items from query collection {collectionId}");
        }

        await db.CollectionItems
            .Where(c => c.Username == key && c.CollectionId == collectionId && c.ItemId == itemId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> CountLocalActorsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Actors.CountAsync(a => a.IsLocal && a.Username != SystemActorUsername, cancellationToken);
    }

    private const string SystemActorUsername = "sys";

    private async Task AddFollowAsync(string username, string actorId, FollowType type, CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var exists = await db.Follows.AnyAsync(
            f => f.Username == key && f.ActorId == actorId && f.FollowType == type,
            cancellationToken);
        if (!exists)
        {
            db.Follows.Add(new FollowEntity { Username = key, ActorId = actorId, FollowType = type });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RemoveFollowAsync(string username, string actorId, FollowType type, CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        await db.Follows
            .Where(f => f.Username == key && f.ActorId == actorId && f.FollowType == type)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private bool IsLocalActor(string? actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId)) return false;
        var baseUrl = _serverOptions.BaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("ActivityPub BaseUrl is not configured; actor locality cannot be determined accurately");
            return false;
        }
        return actorId.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase);
    }

    private string? ExtractDomain(string? actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId)) return null;
        if (Uri.TryCreate(actorId, UriKind.Absolute, out var uri))
            return uri.Host.ToLowerInvariant();
        _logger.LogWarning("Could not extract domain from actor ID {ActorId}", actorId);
        return null;
    }
}
