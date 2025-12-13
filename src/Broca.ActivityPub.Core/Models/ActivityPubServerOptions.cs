namespace Broca.ActivityPub.Core.Models;

/// <summary>
/// Configuration options for the ActivityPub server
/// </summary>
public class ActivityPubServerOptions
{
    /// <summary>
    /// Broca namespace URI for custom JSON-LD extensions
    /// </summary>
    public const string BrocaNamespace = "https://broca-activitypub.org/ns#";

    /// <summary>
    /// Base URL for the server (e.g., https://example.com)
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost";

    /// <summary>
    /// Primary domain for the server (e.g., example.com)
    /// </summary>
    public string PrimaryDomain { get; set; } = "localhost";

    /// <summary>
    /// Server name for display purposes
    /// </summary>
    public string ServerName { get; set; } = "Broca ActivityPub Server";

    /// <summary>
    /// System actor username (default: sys)
    /// </summary>
    /// <remarks>
    /// This actor represents the server itself and can perform federated requests
    /// with a verifiable identity (e.g., sys@example.com)
    /// </remarks>
    public string SystemActorUsername { get; set; } = "sys";

    /// <summary>
    /// Route prefix for ActivityPub endpoints (e.g., "ap", "activitypub", or empty for root)
    /// </summary>
    /// <remarks>
    /// This allows implementors to host ActivityPub under a specific route segment
    /// to avoid conflicts with existing application routes.
    /// Examples: "ap" -> /ap/users/{username}, "" -> /users/{username}
    /// </remarks>
    public string RoutePrefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets the normalized route prefix with leading slash
    /// </summary>
    public string NormalizedRoutePrefix => 
        string.IsNullOrWhiteSpace(RoutePrefix) 
            ? string.Empty 
            : "/" + RoutePrefix.Trim('/');

    /// <summary>
    /// Gets the system actor's full actor ID
    /// </summary>
    public string SystemActorId => $"{BaseUrl?.TrimEnd('/') ?? "http://localhost"}{NormalizedRoutePrefix}/users/{SystemActorUsername}";

    /// <summary>
    /// Gets the system actor's alias (e.g., sys@example.com)
    /// </summary>
    public string SystemActorAlias => $"{SystemActorUsername}@{PrimaryDomain ?? "localhost"}";

    /// <summary>
    /// User agent string for HTTP requests
    /// </summary>
    public string UserAgent { get; set; } = "Broca.ActivityPub.Server/1.0";

    /// <summary>
    /// Whether to auto-accept follow requests
    /// </summary>
    public bool AutoAcceptFollows { get; set; } = true;

    /// <summary>
    /// Whether to require HTTP signatures for inbox delivery
    /// </summary>
    public bool RequireHttpSignatures { get; set; } = true;

    /// <summary>
    /// Whether to enable outbound activity delivery
    /// </summary>
    public bool EnableActivityDelivery { get; set; } = true;

    /// <summary>
    /// Default page size for collections
    /// </summary>
    public int DefaultCollectionPageSize { get; set; } = 20;

    /// <summary>
    /// List of actor IDs authorized to perform administrative operations
    /// </summary>
    /// <remarks>
    /// These actors can send administrative activities (Create/Update/Delete Actor)
    /// to the system actor's inbox. The system actor itself is always authorized.
    /// Example: ["https://example.com/users/admin", "https://example.com/users/moderator"]
    /// </remarks>
    public List<string>? AuthorizedAdminActors { get; set; }

    /// <summary>
    /// Whether to enable administrative operations via ActivityPub
    /// </summary>
    /// <remarks>
    /// When enabled, authorized actors can create, update, and delete users
    /// by posting appropriately signed activities to the system actor's inbox.
    /// This provides a back-channel administrative interface using ActivityPub protocol.
    /// </remarks>
    public bool EnableAdminOperations { get; set; } = false;

    /// <summary>
    /// Admin API token for accessing privileged endpoints
    /// </summary>
    /// <remarks>
    /// When provided in the Authorization header as a Bearer token,
    /// allows access to sensitive information such as actor private keys.
    /// Should be a strong, randomly generated token.
    /// </remarks>
    public string? AdminApiToken { get; set; }
}
