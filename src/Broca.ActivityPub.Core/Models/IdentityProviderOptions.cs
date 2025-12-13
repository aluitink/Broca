namespace Broca.ActivityPub.Core.Models;

/// <summary>
/// Configuration options for identity providers
/// </summary>
public class IdentityProviderOptions
{
    /// <summary>
    /// Simple identity configuration (for single user or static identities)
    /// </summary>
    public SimpleIdentityOptions? SimpleIdentity { get; set; }
}

/// <summary>
/// Configuration for simple identity provider
/// </summary>
public class SimpleIdentityOptions
{
    /// <summary>
    /// Username (without domain, e.g., "alice")
    /// </summary>
    public string? Username { get; set; }

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
    /// Actor type (Person, Organization, Service, Application, Group)
    /// </summary>
    public string ActorType { get; set; } = "Person";

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
    /// Optional: Path to private key PEM file
    /// </summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>
    /// Optional: Path to public key PEM file
    /// </summary>
    public string? PublicKeyPath { get; set; }

    /// <summary>
    /// Optional: Additional profile fields (e.g., website, location)
    /// </summary>
    public Dictionary<string, string>? Fields { get; set; }
}
