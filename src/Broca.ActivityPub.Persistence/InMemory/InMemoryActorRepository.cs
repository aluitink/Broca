using System.Collections.Concurrent;
using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Persistence.InMemory;

/// <summary>
/// In-memory implementation of actor repository for testing and development
/// </summary>
public class InMemoryActorRepository : IActorRepository
{
    private readonly ConcurrentDictionary<string, Actor> _actors = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _followers = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _following = new();
    private readonly ConcurrentDictionary<string, string> _actorIdToUsername = new();

    public Task<Actor?> GetActorByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        _actors.TryGetValue(username.ToLowerInvariant(), out var actor);
        return Task.FromResult(actor);
    }

    public Task<Actor?> GetActorByIdAsync(string actorId, CancellationToken cancellationToken = default)
    {
        if (_actorIdToUsername.TryGetValue(actorId, out var username))
        {
            return GetActorByUsernameAsync(username, cancellationToken);
        }
        return Task.FromResult<Actor?>(null);
    }

    public Task SaveActorAsync(string username, Actor actor, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        _actors[key] = actor;
        
        // Extract and store actor ID for reverse lookup
        if (actor.Id != null)
        {
            _actorIdToUsername[actor.Id] = key;
        }
        
        return Task.CompletedTask;
    }

    public Task DeleteActorAsync(string username, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        _actors.TryRemove(key, out _);
        _followers.TryRemove(key, out _);
        _following.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetFollowersAsync(string username, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        if (_followers.TryGetValue(key, out var followers))
        {
            return Task.FromResult<IEnumerable<string>>(followers.ToList());
        }
        return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
    }

    public Task<IEnumerable<string>> GetFollowingAsync(string username, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        if (_following.TryGetValue(key, out var following))
        {
            return Task.FromResult<IEnumerable<string>>(following.ToList());
        }
        return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
    }

    public Task AddFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        var followers = _followers.GetOrAdd(key, _ => new ConcurrentBag<string>());
        if (!followers.Contains(followerActorId))
        {
            followers.Add(followerActorId);
        }
        return Task.CompletedTask;
    }

    public Task RemoveFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        if (_followers.TryGetValue(key, out var followers))
        {
            _followers[key] = new ConcurrentBag<string>(followers.Where(f => f != followerActorId));
        }
        return Task.CompletedTask;
    }

    public Task AddFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        var following = _following.GetOrAdd(key, _ => new ConcurrentBag<string>());
        if (!following.Contains(followingActorId))
        {
            following.Add(followingActorId);
        }
        return Task.CompletedTask;
    }

    public Task RemoveFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
    {
        var key = username.ToLowerInvariant();
        if (_following.TryGetValue(key, out var following))
        {
            _following[key] = new ConcurrentBag<string>(following.Where(f => f != followingActorId));
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears all stored data from the repository. Used for testing.
    /// </summary>
    public void Clear()
    {
        _actors.Clear();
        _followers.Clear();
        _following.Clear();
        _actorIdToUsername.Clear();
    }
}
