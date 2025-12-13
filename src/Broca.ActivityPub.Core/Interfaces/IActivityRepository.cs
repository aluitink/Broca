using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Repository interface for activity storage
/// </summary>
public interface IActivityRepository
{
    /// <summary>
    /// Saves an activity to the inbox
    /// </summary>
    Task SaveInboxActivityAsync(string username, string activityId, IObjectOrLink activity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an activity to the outbox
    /// </summary>
    Task SaveOutboxActivityAsync(string username, string activityId, IObjectOrLink activity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets inbox activities for an actor
    /// </summary>
    Task<IEnumerable<IObjectOrLink>> GetInboxActivitiesAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets outbox activities for an actor
    /// </summary>
    Task<IEnumerable<IObjectOrLink>> GetOutboxActivitiesAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an activity by ID
    /// </summary>
    Task<IObjectOrLink?> GetActivityByIdAsync(string activityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an activity
    /// </summary>
    Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of inbox activities
    /// </summary>
    Task<int> GetInboxCountAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of outbox activities
    /// </summary>
    Task<int> GetOutboxCountAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets replies to a specific object
    /// </summary>
    Task<IEnumerable<IObjectOrLink>> GetRepliesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of replies for an object
    /// </summary>
    Task<int> GetRepliesCountAsync(string objectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets likes for a specific object
    /// </summary>
    Task<IEnumerable<IObjectOrLink>> GetLikesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of likes for an object
    /// </summary>
    Task<int> GetLikesCountAsync(string objectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets shares (announces) for a specific object
    /// </summary>
    Task<IEnumerable<IObjectOrLink>> GetSharesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of shares for an object
    /// </summary>
    Task<int> GetSharesCountAsync(string objectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all objects liked by a specific actor
    /// </summary>
    Task<IEnumerable<IObjectOrLink>> GetLikedByActorAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of objects liked by an actor
    /// </summary>
    Task<int> GetLikedByActorCountAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all objects shared by a specific actor
    /// </summary>
    Task<IEnumerable<IObjectOrLink>> GetSharedByActorAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of objects shared by an actor
    /// </summary>
    Task<int> GetSharedByActorCountAsync(string username, CancellationToken cancellationToken = default);
}
