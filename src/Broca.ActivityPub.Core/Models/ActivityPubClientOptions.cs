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
    /// Can be provided directly or fetched using ApiKey.
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
    /// API key for fetching actor identity and private key from the server
    /// </summary>
    /// <remarks>
    /// When provided, the client will automatically fetch the actor's
    /// private key from the server using this API key as a Bearer token.
    /// This key will be sent in the Authorization header when requesting
    /// the actor profile. The server must be configured with a matching
    /// AdminApiToken to return the private key.
    /// </remarks>
    public string? ApiKey { get; set; }

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
    /// <remarks>
    /// Authenticated mode requires either:
    /// 1. ActorId + PrivateKeyPem + PublicKeyId (direct private key - no server fetch needed)
    /// 2. ActorId + ApiKey (will fetch private key from server via InitializeAsync)
    /// </remarks>
    public bool IsAuthenticated => 
        !string.IsNullOrWhiteSpace(ActorId) 
        && (HasPrivateKey || HasApiKey);
    
    /// <summary>
    /// Gets whether the client has a private key configured directly
    /// </summary>
    private bool HasPrivateKey => 
        !string.IsNullOrWhiteSpace(PrivateKeyPem) 
        && !string.IsNullOrWhiteSpace(PublicKeyId);
    
    /// <summary>
    /// Gets whether the client has an API key configured
    /// </summary>
    private bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
    
    /// <summary>
    /// Gets whether the client needs to initialize by fetching credentials from the server
    /// </summary>
    /// <remarks>
    /// Returns true when ApiKey is provided but PrivateKeyPem is not yet fetched.
    /// After calling InitializeAsync(), this will return false.
    /// </remarks>
    public bool RequiresInitialization => 
        !string.IsNullOrWhiteSpace(ActorId) 
        && HasApiKey
        && !HasPrivateKey;
}
