namespace Broca.ActivityPub.Core.Models;

/// <summary>
/// Configuration options for the ActivityPub client
/// </summary>
public class ActivityPubClientOptions
{
    /// <summary>
    /// The ActivityPub actor ID for authenticated requests
    /// </summary>
    /// <remarks>
    /// If null, the client operates in anonymous mode
    /// </remarks>
    public string? ActorId { get; set; }

    /// <summary>
    /// PEM-encoded private key for signing HTTP requests
    /// </summary>
    /// <remarks>
    /// Required for authenticated requests. Must match the public key
    /// advertised in the actor's profile.
    /// </remarks>
    public string? PrivateKeyPem { get; set; }

    /// <summary>
    /// The public key ID (typically actorId#main-key)
    /// </summary>
    /// <remarks>
    /// Used in the HTTP signature keyId parameter
    /// </remarks>
    public string? PublicKeyId { get; set; }

    /// <summary>
    /// User agent string for HTTP requests
    /// </summary>
    public string UserAgent { get; set; } = "Broca.ActivityPub.Client/1.0";

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to cache fetched objects
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache expiration time in minutes
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets whether the client is configured for authenticated mode
    /// </summary>
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(ActorId) 
        && !string.IsNullOrWhiteSpace(PrivateKeyPem) 
        && !string.IsNullOrWhiteSpace(PublicKeyId);
}
