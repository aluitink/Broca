namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Factory for creating ActivityBuilder instances
/// </summary>
public interface IActivityBuilderFactory
{
    /// <summary>
    /// Creates an activity builder for a given actor
    /// </summary>
    /// <param name="actorId">The full actor ID (e.g., https://example.com/users/alice)</param>
    /// <returns>Activity builder anchored to the actor</returns>
    IActivityBuilder CreateForActor(string actorId);

    /// <summary>
    /// Creates an activity builder for a username on this server
    /// </summary>
    /// <param name="username">The username (e.g., alice)</param>
    /// <returns>Activity builder anchored to the local actor</returns>
    IActivityBuilder CreateForUsername(string username);

    /// <summary>
    /// Creates an activity builder for the system actor
    /// </summary>
    /// <returns>Activity builder anchored to the system actor</returns>
    IActivityBuilder CreateForSystemActor();
}
