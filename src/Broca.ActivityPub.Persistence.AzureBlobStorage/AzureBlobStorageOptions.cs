namespace Broca.ActivityPub.Persistence.AzureBlobStorage;

/// <summary>
/// Configuration options for Azure Blob Storage
/// </summary>
public class AzureBlobStorageOptions
{
    /// <summary>
    /// Azure Storage connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Container name for storing blobs
    /// </summary>
    public string ContainerName { get; set; } = "activitypub-blobs";

    /// <summary>
    /// Base URL for accessing blobs (e.g., via CDN)
    /// If not specified, the default Azure Blob Storage URL will be used
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Whether to create the container if it doesn't exist
    /// </summary>
    public bool CreateContainerIfNotExists { get; set; } = true;

    /// <summary>
    /// Whether blobs should be publicly accessible
    /// </summary>
    public bool PublicAccess { get; set; } = true;
}
