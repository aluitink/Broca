using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Repository interface for actor storage
/// </summary>
public interface IActorRepository
{
    /// <summary>
    /// Gets an actor by username
    /// </summary>
    Task<Actor?> GetActorByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an actor by ID (URI)
    /// </summary>
    Task<Actor?> GetActorByIdAsync(string actorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates an actor
    /// </summary>
    Task SaveActorAsync(string username, Actor actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an actor
    /// </summary>
    Task DeleteActorAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all followers for an actor
    /// </summary>
    Task<IEnumerable<string>> GetFollowersAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all following for an actor
    /// </summary>
    Task<IEnumerable<string>> GetFollowingAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a follower
    /// </summary>
    Task AddFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a follower
    /// </summary>
    Task RemoveFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a following
    /// </summary>
    Task AddFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a following
    /// </summary>
    Task RemoveFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default);
}
