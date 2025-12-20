using Broca.ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Broca.ActivityPub.Persistence.FileSystem;

/// <summary>
/// File system implementation of blob storage
/// </summary>
public class FileSystemBlobStorageService : IBlobStorageService
{
    private readonly FileSystemBlobStorageOptions _options;
    private readonly ILogger<FileSystemBlobStorageService> _logger;

    // Map of file extensions to MIME types
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        { ".jpg", "image/jpeg" }, { ".jpeg", "image/jpeg" }, { ".png", "image/png" },
        { ".gif", "image/gif" }, { ".webp", "image/webp" }, { ".svg", "image/svg+xml" },
        { ".bmp", "image/bmp" }, { ".ico", "image/x-icon" },
        
        // Videos
        { ".mp4", "video/mp4" }, { ".webm", "video/webm" }, { ".ogv", "video/ogg" },
        { ".avi", "video/x-msvideo" }, { ".mov", "video/quicktime" },
        
        // Audio
        { ".mp3", "audio/mpeg" }, { ".ogg", "audio/ogg" }, { ".wav", "audio/wav" },
        { ".flac", "audio/flac" }, { ".m4a", "audio/mp4" },
        
        // Documents
        { ".pdf", "application/pdf" }, { ".txt", "text/plain" },
        { ".html", "text/html" }, { ".json", "application/json" }
    };

    public FileSystemBlobStorageService(
        IOptions<FileSystemBlobStorageOptions> options,
        ILogger<FileSystemBlobStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Ensure blobs directory exists
        Directory.CreateDirectory(_options.DataPath);

        _logger.LogInformation("FileSystemBlobStorageService initialized with data path: {DataPath}", _options.DataPath);
    }

    public async Task<string> StoreBlobAsync(
        string username,
        string blobId,
        Stream content,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobId);
        ArgumentNullException.ThrowIfNull(content);

        _logger.LogDebug("Storing blob {BlobId} for user {Username}", blobId, username);

        var blobPath = GetBlobPath(username, blobId);
        var directory = Path.GetDirectoryName(blobPath)!;
        Directory.CreateDirectory(directory);

        // Store the blob content
        using (var fileStream = new FileStream(blobPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        // Store metadata
        if (!string.IsNullOrEmpty(contentType))
        {
            var metadataPath = blobPath + ".meta";
            var metadata = new BlobMetadata
            {
                ContentType = contentType,
                CreatedAt = DateTime.UtcNow,
                SizeBytes = new FileInfo(blobPath).Length
            };
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata), cancellationToken);
        }

        return BuildBlobUrl(username, blobId);
    }

    public async Task<(Stream Content, string ContentType)?> GetBlobAsync(
        string username,
        string blobId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobId);

        _logger.LogDebug("Retrieving blob {BlobId} for user {Username}", blobId, username);

        var blobPath = GetBlobPath(username, blobId);

        if (!File.Exists(blobPath))
        {
            _logger.LogDebug("Blob {BlobId} not found at path {Path}", blobId, blobPath);
            return null;
        }

        // Get content type from metadata or infer from extension
        var contentType = await GetContentTypeAsync(blobPath, cancellationToken);

        // Return a file stream
        var stream = new FileStream(blobPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        
        return (stream, contentType);
    }

    public Task DeleteBlobAsync(
        string username,
        string blobId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobId);

        _logger.LogDebug("Deleting blob {BlobId} for user {Username}", blobId, username);

        var blobPath = GetBlobPath(username, blobId);
        var metadataPath = blobPath + ".meta";

        if (File.Exists(blobPath))
        {
            File.Delete(blobPath);
        }

        if (File.Exists(metadataPath))
        {
            File.Delete(metadataPath);
        }

        return Task.CompletedTask;
    }

    public Task<bool> BlobExistsAsync(
        string username,
        string blobId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobId);

        var blobPath = GetBlobPath(username, blobId);
        return Task.FromResult(File.Exists(blobPath));
    }

    public string BuildBlobUrl(string username, string blobId)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var routePrefix = _options.RoutePrefix.TrimStart('/').TrimEnd('/');
        
        return $"{baseUrl}/{routePrefix}/{username}/{blobId}";
    }

    private string GetBlobPath(string username, string blobId)
    {
        var sanitizedUsername = SanitizePathComponent(username);
        var sanitizedBlobId = SanitizePathComponent(blobId);

        if (_options.OrganizeByDate)
        {
            var now = DateTime.UtcNow;
            return Path.Combine(
                _options.DataPath,
                sanitizedUsername,
                now.Year.ToString("D4"),
                now.Month.ToString("D2"),
                now.Day.ToString("D2"),
                sanitizedBlobId
            );
        }

        return Path.Combine(_options.DataPath, sanitizedUsername, sanitizedBlobId);
    }

    private async Task<string> GetContentTypeAsync(string blobPath, CancellationToken cancellationToken)
    {
        // Try to get from metadata first
        var metadataPath = blobPath + ".meta";
        if (File.Exists(metadataPath))
        {
            try
            {
                var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                var metadata = JsonSerializer.Deserialize<BlobMetadata>(metadataJson);
                if (metadata?.ContentType != null)
                {
                    return metadata.ContentType;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read metadata for blob at {Path}", blobPath);
            }
        }

        // Fall back to extension-based detection
        var extension = Path.GetExtension(blobPath);
        if (MimeTypes.TryGetValue(extension, out var mimeType))
        {
            return mimeType;
        }

        return "application/octet-stream";
    }

    private static string SanitizePathComponent(string component)
    {
        // Remove any path traversal attempts and invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(component
            .Where(c => !invalidChars.Contains(c) && c != '.' && c != '/')
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ArgumentException($"Invalid path component: {component}");
        }

        return sanitized;
    }

    private class BlobMetadata
    {
        public string ContentType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public long SizeBytes { get; set; }
    }
}
