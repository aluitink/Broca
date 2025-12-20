namespace Broca.ActivityPub.Persistence.FileSystem;

/// <summary>
/// Configuration options for File System blob storage
/// </summary>
public class FileSystemBlobStorageOptions
{
    /// <summary>
    /// Root directory path for storing blobs
    /// </summary>
    public string DataPath { get; set; } = "data/blobs";

    /// <summary>
    /// Base URL for accessing blobs
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Route prefix for blob URLs (e.g., "/blobs" or "/media")
    /// </summary>
    public string RoutePrefix { get; set; } = "/blobs";

    /// <summary>
    /// Maximum file size in bytes (default: 50MB)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// Whether to organize blobs by date (username/YYYY/MM/DD/blob-id)
    /// </summary>
    public bool OrganizeByDate { get; set; } = false;
}
