using Broca.ActivityPub.Core.Models;
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
    /// Gets a page of followers for an actor
    /// </summary>
    Task<IEnumerable<string>> GetFollowersAsync(string username, int limit, int offset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total number of followers for an actor
    /// </summary>
    Task<int> GetFollowersCountAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all following for an actor
    /// </summary>
    Task<IEnumerable<string>> GetFollowingAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a page of following for an actor
    /// </summary>
    Task<IEnumerable<string>> GetFollowingAsync(string username, int limit, int offset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total number of actors being followed
    /// </summary>
    Task<int> GetFollowingCountAsync(string username, CancellationToken cancellationToken = default);

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

    // Custom Collections

    /// <summary>
    /// Gets all custom collection definitions for an actor
    /// </summary>
    Task<IEnumerable<CustomCollectionDefinition>> GetCollectionDefinitionsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific collection definition by ID
    /// </summary>
    Task<CustomCollectionDefinition?> GetCollectionDefinitionAsync(string username, string collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a collection definition
    /// </summary>
    Task SaveCollectionDefinitionAsync(string username, CustomCollectionDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a collection definition
    /// </summary>
    Task DeleteCollectionDefinitionAsync(string username, string collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an item to a manual collection
    /// </summary>
    Task AddToCollectionAsync(string username, string collectionId, string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an item from a manual collection
    /// </summary>
    Task RemoveFromCollectionAsync(string username, string collectionId, string itemId, CancellationToken cancellationToken = default);

    // Pending Followers (for manuallyApprovesFollowers = true)

    /// <summary>
    /// Gets all pending follower requests for an actor awaiting manual approval
    /// </summary>
    Task<IEnumerable<string>> GetPendingFollowersAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a follower request to the pending list
    /// </summary>
    Task AddPendingFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a follower request from the pending list
    /// </summary>
    Task RemovePendingFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default);

    // Pending Following (outgoing Follow sent, awaiting remote Accept)

    /// <summary>
    /// Gets all outgoing follow requests that have been sent but not yet accepted by the remote actor
    /// </summary>
    Task<IEnumerable<string>> GetPendingFollowingAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an outgoing Follow that has been successfully delivered to the remote actor's inbox but not yet accepted
    /// </summary>
    Task AddPendingFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an entry from the pending-following list (on Accept, Reject, Undo, or permanent delivery failure)
    /// </summary>
    Task RemovePendingFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all local usernames
    /// </summary>
    Task<IEnumerable<string>> GetAllLocalUsernamesAsync(CancellationToken cancellationToken = default);
}
