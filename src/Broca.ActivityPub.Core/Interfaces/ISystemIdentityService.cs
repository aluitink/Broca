using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Service for managing the server's system identity
/// </summary>
/// <remarks>
/// The system identity (typically sys@domain) represents the server itself
/// and can perform authenticated requests to other servers with a verifiable identity.
/// </remarks>
public interface ISystemIdentityService
{
    /// <summary>
    /// Gets the system actor
    /// </summary>
    Task<Actor> GetSystemActorAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the system actor exists in the repository
    /// </summary>
    Task EnsureSystemActorAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the system actor's private key for signing requests
    /// </summary>
    Task<string> GetSystemPrivateKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the system actor ID
    /// </summary>
    string SystemActorId { get; }

    /// <summary>
    /// Gets the system actor alias (e.g., sys@example.com)
    /// </summary>
    string SystemActorAlias { get; }
}
