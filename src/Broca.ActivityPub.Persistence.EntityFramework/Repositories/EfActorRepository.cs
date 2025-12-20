using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.EntityFramework.Entities;
using KristofferStrube.ActivityStreams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Broca.ActivityPub.Persistence.EntityFramework.Repositories;

/// <summary>
/// Entity Framework implementation of IActorRepository
/// </summary>
public class EfActorRepository : IActorRepository
{
    private readonly ActivityPubDbContext _context;
    private readonly ILogger<EfActorRepository> _logger;

    public EfActorRepository(ActivityPubDbContext context, ILogger<EfActorRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Actor?> GetActorByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting actor by username: {Username}", username);
        
        var entity = await _context.Actors
            .FirstOrDefaultAsync(a => a.Username == username, cancellationToken);

        if (entity == null)
            return null;

        return DeserializeActor(entity.ActorJson);
    }

    public async Task<Actor?> GetActorByIdAsync(string actorId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting actor by ID: {ActorId}", actorId);
        
        var entity = await _context.Actors
            .FirstOrDefaultAsync(a => a.ActorId == actorId, cancellationToken);

        if (entity == null)
            return null;

        return DeserializeActor(entity.ActorJson);
    }

    public async Task SaveActorAsync(string username, Actor actor, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Saving actor: {Username}", username);
        
        var json = SerializeActor(actor);
        var actorId = actor.Id?.ToString() ?? string.Empty;

        var entity = await _context.Actors
            .FirstOrDefaultAsync(a => a.Username == username, cancellationToken);

        if (entity == null)
        {
            entity = new ActorEntity
            {
                Username = username,
                ActorId = actorId,
                ActorJson = json,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Actors.Add(entity);
        }
        else
        {
            entity.ActorId = actorId;
            entity.ActorJson = json;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteActorAsync(string username, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting actor: {Username}", username);
        
        var entity = await _context.Actors
            .FirstOrDefaultAsync(a => a.Username == username, cancellationToken);

        if (entity != null)
        {
            _context.Actors.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<string>> GetFollowersAsync(string username, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting followers for: {Username}", username);
        
        return await _context.Followers
            .Where(f => f.Username == username)
            .Select(f => f.FollowerActorId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetFollowingAsync(string username, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting following for: {Username}", username);
        
        return await _context.Following
            .Where(f => f.Username == username)
            .Select(f => f.FollowingActorId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Adding follower {FollowerActorId} to {Username}", followerActorId, username);
        
        var exists = await _context.Followers
            .AnyAsync(f => f.Username == username && f.FollowerActorId == followerActorId, cancellationToken);

        if (!exists)
        {
            _context.Followers.Add(new FollowerEntity
            {
                Username = username,
                FollowerActorId = followerActorId,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Removing follower {FollowerActorId} from {Username}", followerActorId, username);
        
        var entity = await _context.Followers
            .FirstOrDefaultAsync(f => f.Username == username && f.FollowerActorId == followerActorId, cancellationToken);

        if (entity != null)
        {
            _context.Followers.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Adding following {FollowingActorId} to {Username}", followingActorId, username);
        
        var exists = await _context.Following
            .AnyAsync(f => f.Username == username && f.FollowingActorId == followingActorId, cancellationToken);

        if (!exists)
        {
            _context.Following.Add(new FollowingEntity
            {
                Username = username,
                FollowingActorId = followingActorId,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Removing following {FollowingActorId} from {Username}", followingActorId, username);
        
        var entity = await _context.Following
            .FirstOrDefaultAsync(f => f.Username == username && f.FollowingActorId == followingActorId, cancellationToken);

        if (entity != null)
        {
            _context.Following.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> GetFollowersCountAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.Followers
            .CountAsync(f => f.Username == username, cancellationToken);
    }

    public async Task<int> GetFollowingCountAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.Following
            .CountAsync(f => f.Username == username, cancellationToken);
    }

    public async Task<IEnumerable<CustomCollectionDefinition>> GetCollectionDefinitionsAsync(string username, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting collection definitions for: {Username}", username);
        
        var entities = await _context.CollectionDefinitions
            .Where(c => c.Username == username)
            .ToListAsync(cancellationToken);

        return entities.Select(e => DeserializeCollectionDefinition(e.DefinitionJson)).Where(c => c != null).Cast<CustomCollectionDefinition>();
    }

    public async Task<CustomCollectionDefinition?> GetCollectionDefinitionAsync(string username, string collectionId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting collection definition {CollectionId} for {Username}", collectionId, username);
        
        var entity = await _context.CollectionDefinitions
            .FirstOrDefaultAsync(c => c.Username == username && c.CollectionId == collectionId, cancellationToken);

        if (entity == null)
            return null;

        return DeserializeCollectionDefinition(entity.DefinitionJson);
    }

    public async Task SaveCollectionDefinitionAsync(string username, CustomCollectionDefinition definition, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Saving collection definition {CollectionId} for {Username}", definition.Id, username);
        
        var json = SerializeCollectionDefinition(definition);

        var entity = await _context.CollectionDefinitions
            .FirstOrDefaultAsync(c => c.Username == username && c.CollectionId == definition.Id, cancellationToken);

        if (entity == null)
        {
            entity = new CollectionDefinitionEntity
            {
                Username = username,
                CollectionId = definition.Id,
                DefinitionJson = json,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.CollectionDefinitions.Add(entity);
        }
        else
        {
            entity.DefinitionJson = json;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCollectionDefinitionAsync(string username, string collectionId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting collection definition {CollectionId} for {Username}", collectionId, username);
        
        var entity = await _context.CollectionDefinitions
            .FirstOrDefaultAsync(c => c.Username == username && c.CollectionId == collectionId, cancellationToken);

        if (entity != null)
        {
            _context.CollectionDefinitions.Remove(entity);
            
            // Also remove all items from this collection
            var items = await _context.CollectionItems
                .Where(i => i.Username == username && i.CollectionId == collectionId)
                .ToListAsync(cancellationToken);
            
            _context.CollectionItems.RemoveRange(items);
            
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddToCollectionAsync(string username, string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Adding item {ItemId} to collection {CollectionId} for {Username}", itemId, collectionId, username);
        
        var exists = await _context.CollectionItems
            .AnyAsync(i => i.Username == username && i.CollectionId == collectionId && i.ItemId == itemId, cancellationToken);

        if (!exists)
        {
            _context.CollectionItems.Add(new CollectionItemEntity
            {
                Username = username,
                CollectionId = collectionId,
                ItemId = itemId,
                AddedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveFromCollectionAsync(string username, string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Removing item {ItemId} from collection {CollectionId} for {Username}", itemId, collectionId, username);
        
        var entity = await _context.CollectionItems
            .FirstOrDefaultAsync(i => i.Username == username && i.CollectionId == collectionId && i.ItemId == itemId, cancellationToken);

        if (entity != null)
        {
            _context.CollectionItems.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<Actor>> SearchActorsAsync(string query, int limit = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching actors with query: {Query}", query);
        
        // TODO: Implement full-text search based on the database provider
        var entities = await _context.Actors
            .Where(a => a.Username.Contains(query))
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(e => DeserializeActor(e.ActorJson)).Where(a => a != null).Cast<Actor>();
    }

    private static string SerializeActor(Actor actor)
    {
        // TODO: Implement proper Actor serialization
        return JsonSerializer.Serialize(actor);
    }

    private static Actor? DeserializeActor(string json)
    {
        // TODO: Implement proper Actor deserialization
        return JsonSerializer.Deserialize<Actor>(json);
    }

    private static string SerializeCollectionDefinition(CustomCollectionDefinition definition)
    {
        // TODO: Implement proper CustomCollectionDefinition serialization
        return JsonSerializer.Serialize(definition);
    }

    private static CustomCollectionDefinition? DeserializeCollectionDefinition(string json)
    {
        // TODO: Implement proper CustomCollectionDefinition deserialization
        return JsonSerializer.Deserialize<CustomCollectionDefinition>(json);
    }
}
