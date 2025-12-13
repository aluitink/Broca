namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Interface for blob storage operations
/// </summary>
/// <remarks>
/// Provides abstraction for storing and retrieving binary objects like images,
/// videos, and other media attachments used in ActivityPub objects.
/// </remarks>
public interface IBlobStorageService
{
    /// <summary>
    /// Stores a blob and returns the public URL
    /// </summary>
    /// <param name="username">Username of the actor owning the blob</param>
    /// <param name="blobId">Unique identifier for the blob</param>
    /// <param name="content">Stream containing the blob data</param>
    /// <param name="contentType">MIME type of the content (e.g., "image/png")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Public URL where the blob can be accessed</returns>
    Task<string> StoreBlobAsync(string username, string blobId, Stream content, string? contentType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a blob by its ID
    /// </summary>
    /// <param name="username">Username of the actor owning the blob</param>
    /// <param name="blobId">Unique identifier for the blob</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of stream and content type, or null if not found</returns>
    Task<(Stream Content, string ContentType)?> GetBlobAsync(string username, string blobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a blob
    /// </summary>
    /// <param name="username">Username of the actor owning the blob</param>
    /// <param name="blobId">Unique identifier for the blob</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteBlobAsync(string username, string blobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a blob exists
    /// </summary>
    /// <param name="username">Username of the actor owning the blob</param>
    /// <param name="blobId">Unique identifier for the blob</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> BlobExistsAsync(string username, string blobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the public URL for a blob
    /// </summary>
    /// <param name="username">Username of the actor owning the blob</param>
    /// <param name="blobId">Unique identifier for the blob</param>
    /// <returns>Public URL for the blob</returns>
    string BuildBlobUrl(string username, string blobId);
}
