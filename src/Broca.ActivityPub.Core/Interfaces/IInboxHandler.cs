using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Handler interface for processing inbox activities
/// </summary>
public interface IInboxHandler
{
    /// <summary>
    /// Handles an activity received in an actor's inbox
    /// </summary>
    /// <param name="username">The username of the inbox owner</param>
    /// <param name="activity">The activity to process</param>
    /// <param name="isBearerTokenAuthenticated">True if the request was authenticated via bearer token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the activity was handled successfully</returns>
    Task<bool> HandleActivityAsync(string username, IObjectOrLink activity, bool isBearerTokenAuthenticated = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies HTTP signature of an incoming request
    /// </summary>
    Task<bool> VerifySignatureAsync(string signature, string requestTarget, string host, string date, string digest, CancellationToken cancellationToken = default);
}
