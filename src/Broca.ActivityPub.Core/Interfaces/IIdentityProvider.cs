using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Interface for providing identity information to the ActivityPub server
/// </summary>
/// <remarks>
/// Implement this interface to provide actor identities from your existing
/// application (database, configuration, external service, etc.).
/// The system will automatically create ActivityPub actors and serve
/// all necessary endpoints (WebFinger, Actor, Inbox, Outbox).
/// </remarks>
public interface IIdentityProvider
{
    /// <summary>
    /// Gets all usernames that should be available via ActivityPub
    /// </summary>
    /// <remarks>
    /// Called during startup to initialize actors.
    /// For static identities, return a fixed list.
    /// For dynamic identities, you can query your database or service.
    /// </remarks>
    Task<IEnumerable<string>> GetUsernamesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets identity details for a specific username
    /// </summary>
    /// <param name="username">The username (without domain)</param>
    /// <returns>Identity details or null if user doesn't exist</returns>
    Task<IdentityDetails?> GetIdentityDetailsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a username exists and should be served via ActivityPub
    /// </summary>
    /// <param name="username">The username to check</param>
    /// <returns>True if the identity exists, false otherwise</returns>
    Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default);
}

/// <summary>
/// Details for an identity that will be exposed via ActivityPub
/// </summary>
public class IdentityDetails
{
    /// <summary>
    /// Username (without domain, e.g., "alice")
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// Display name (e.g., "Alice Smith")
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Bio/summary text
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Profile image URL
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Header/banner image URL
    /// </summary>
    public string? HeaderUrl { get; set; }

    /// <summary>
    /// Actor type (defaults to Person)
    /// </summary>
    public ActorType ActorType { get; set; } = ActorType.Person;

    /// <summary>
    /// Whether this is a bot account
    /// </summary>
    public bool IsBot { get; set; }

    /// <summary>
    /// Whether the account is locked (requires follow approval)
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Whether the account is discoverable in directories
    /// </summary>
    public bool IsDiscoverable { get; set; } = true;

    /// <summary>
    /// Optional: Pre-existing public/private key pair (PEM format)
    /// If not provided, keys will be auto-generated
    /// </summary>
    public KeyPair? Keys { get; set; }

    /// <summary>
    /// Optional: Additional profile fields (e.g., website, location)
    /// </summary>
    public Dictionary<string, string>? Fields { get; set; }
}

/// <summary>
/// RSA key pair for actor signing
/// </summary>
public class KeyPair
{
    /// <summary>
    /// Public key in PEM format
    /// </summary>
    public required string PublicKey { get; set; }

    /// <summary>
    /// Private key in PEM format
    /// </summary>
    public required string PrivateKey { get; set; }
}

/// <summary>
/// Actor type enumeration
/// </summary>
public enum ActorType
{
    Person,
    Organization,
    Service,
    Application,
    Group
}
