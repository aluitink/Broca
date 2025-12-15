using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Client interface for interacting with ActivityPub servers
/// </summary>
/// <remarks>
/// Supports both anonymous and authenticated usage:
/// - Anonymous: Browse public content without authentication
/// - Authenticated: Use actor credentials to sign requests
/// 
/// Authentication can be configured via:
/// 1. Direct private key: Provide ActorId, PrivateKeyPem, and PublicKeyId
/// 2. API key: Provide ActorId and ApiKey, then call InitializeAsync() to fetch private key
/// </remarks>
public interface IActivityPubClient
{
    /// <summary>
    /// Gets the current authenticated actor, or null if anonymous
    /// </summary>
    string? ActorId { get; }

    /// <summary>
    /// Initializes the client by fetching credentials from the server using an API key
    /// </summary>
    /// <remarks>
    /// Call this method when the client is configured with ActorId and ApiKey.
    /// It will fetch the actor's private key from the server using the API key.
    /// If the client already has a private key or doesn't have an API key, this is a no-op.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or fetches the actor for the authenticated user
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Actor object, or null if using anonymously</returns>
    Task<Actor?> GetSelfAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches an actor by their URI
    /// </summary>
    /// <param name="actorUri">URI of the actor</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Actor object</returns>
    Task<Actor> GetActorAsync(Uri actorUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves an actor by their alias (e.g., @user@domain.tld)
    /// </summary>
    /// <param name="alias">Actor alias</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Actor object</returns>
    Task<Actor> GetActorByAliasAsync(string alias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches an ActivityPub object from a URI
    /// </summary>
    /// <typeparam name="T">Type of object to fetch</typeparam>
    /// <param name="uri">URI of the object</param>
    /// <param name="useCache">Whether to use cached version if available</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The fetched object, or null if not found</returns>
    Task<T?> GetAsync<T>(Uri uri, bool useCache = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an ActivityPub object to a URI
    /// </summary>
    /// <typeparam name="T">Type of object to post</typeparam>
    /// <param name="uri">Target URI</param>
    /// <param name="obj">Object to post</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP response</returns>
    Task<HttpResponseMessage> PostAsync<T>(Uri uri, T obj, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a collection with pagination support
    /// </summary>
    /// <typeparam name="T">Type of items in the collection</typeparam>
    /// <param name="collectionUri">URI of the collection</param>
    /// <param name="limit">Maximum number of items to fetch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Enumerable of collection items</returns>
    IAsyncEnumerable<T> GetCollectionAsync<T>(Uri collectionUri, int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an activity builder anchored to the authenticated actor's identity
    /// </summary>
    /// <returns>Activity builder instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if client is not authenticated</exception>
    IActivityBuilder CreateActivityBuilder();

    /// <summary>
    /// Posts an activity to the authenticated user's outbox
    /// </summary>
    /// <param name="activity">The activity to post</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP response from the outbox post</returns>
    /// <exception cref="InvalidOperationException">Thrown if client is not authenticated</exception>
    Task<HttpResponseMessage> PostToOutboxAsync(Activity activity, CancellationToken cancellationToken = default);
}

