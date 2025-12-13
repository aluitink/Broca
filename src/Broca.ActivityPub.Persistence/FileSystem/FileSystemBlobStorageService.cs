using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Broca.ActivityPub.Persistence.FileSystem;

/// <summary>
/// File system implementation of blob storage
/// </summary>
public class FileSystemBlobStorageService : IBlobStorageService
{
    private readonly string _dataPath;
    private readonly string _baseUrl;
    private readonly string _routePrefix;
    private readonly ILogger<FileSystemBlobStorageService> _logger;

    // Map of file extensions to MIME types
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".gif", "image/gif" },
        { ".webp", "image/webp" },
        { ".svg", "image/svg+xml" },
        { ".bmp", "image/bmp" },
        { ".ico", "image/x-icon" },
        
        // Videos
        { ".mp4", "video/mp4" },
        { ".webm", "video/webm" },
        { ".ogv", "video/ogg" },
        { ".avi", "video/x-msvideo" },
        { ".mov", "video/quicktime" },
        { ".mkv", "video/x-matroska" },
        
        // Audio
        { ".mp3", "audio/mpeg" },
        { ".ogg", "audio/ogg" },
        { ".wav", "audio/wav" },
        { ".flac", "audio/flac" },
        { ".m4a", "audio/mp4" },
        
        // Documents
        { ".pdf", "application/pdf" },
        { ".txt", "text/plain" },
        { ".html", "text/html" },
        { ".json", "application/json" },
        { ".xml", "application/xml" }
    };

    public FileSystemBlobStorageService(
        IOptions<FileSystemPersistenceOptions> persistenceOptions,
        IOptions<ActivityPubServerOptions> serverOptions,
        ILogger<FileSystemBlobStorageService> logger)
    {
        ArgumentNullException.ThrowIfNull(persistenceOptions?.Value);
        ArgumentNullException.ThrowIfNull(serverOptions?.Value);
        ArgumentNullException.ThrowIfNull(logger);

        _dataPath = persistenceOptions.Value.DataPath 
            ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        _baseUrl = serverOptions.Value.BaseUrl?.TrimEnd('/') ?? "http://localhost";
        _routePrefix = serverOptions.Value.NormalizedRoutePrefix;
        _logger = logger;

        // Ensure blobs directory exists
        var blobsPath = Path.Combine(_dataPath, "blobs");
        Directory.CreateDirectory(blobsPath);

        _logger.LogInformation("FileSystemBlobStorageService initialized with data path: {DataPath}", _dataPath);
    }

    /// <inheritdoc/>
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

        var sanitizedUsername = SanitizeUsername(username);
        var sanitizedBlobId = SanitizeBlobId(blobId);
        
        var userBlobDir = Path.Combine(_dataPath, "blobs", sanitizedUsername);
        Directory.CreateDirectory(userBlobDir);

        var blobPath = Path.Combine(userBlobDir, sanitizedBlobId);
        var metadataPath = blobPath + ".meta";

        try
        {
            // Store the blob content
            await using (var fileStream = new FileStream(blobPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await content.CopyToAsync(fileStream, cancellationToken);
            }

            // Determine content type
            var finalContentType = contentType 
                ?? GetContentTypeFromExtension(sanitizedBlobId) 
                ?? "application/octet-stream";

            // Store metadata
            var metadata = new BlobMetadata
            {
                BlobId = blobId,
                ContentType = finalContentType,
                StoredAt = DateTimeOffset.UtcNow,
                FilePath = blobPath
            };

            await File.WriteAllTextAsync(
                metadataPath, 
                System.Text.Json.JsonSerializer.Serialize(metadata), 
                cancellationToken);

            var publicUrl = BuildBlobUrl(username, blobId);
            _logger.LogDebug("Stored blob {BlobId} for user {Username} at {Path}", blobId, username, blobPath);

            return publicUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store blob {BlobId} for user {Username}", blobId, username);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<(Stream Content, string ContentType)?> GetBlobAsync(
        string username, 
        string blobId, 
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobId);

        var sanitizedUsername = SanitizeUsername(username);
        var sanitizedBlobId = SanitizeBlobId(blobId);

        var blobPath = Path.Combine(_dataPath, "blobs", sanitizedUsername, sanitizedBlobId);
        var metadataPath = blobPath + ".meta";

        if (!File.Exists(blobPath))
        {
            _logger.LogDebug("Blob {BlobId} not found for user {Username}", blobId, username);
            return null;
        }

        try
        {
            // Read metadata if it exists
            string contentType = "application/octet-stream";
            if (File.Exists(metadataPath))
            {
                var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                var metadata = System.Text.Json.JsonSerializer.Deserialize<BlobMetadata>(metadataJson);
                contentType = metadata?.ContentType ?? contentType;
            }
            else
            {
                // Fallback to extension-based detection
                contentType = GetContentTypeFromExtension(sanitizedBlobId) ?? contentType;
            }

            // Open file stream (caller is responsible for disposing)
            var fileStream = new FileStream(blobPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            
            _logger.LogDebug("Retrieved blob {BlobId} for user {Username}", blobId, username);
            
            return (fileStream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve blob {BlobId} for user {Username}", blobId, username);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteBlobAsync(
        string username, 
        string blobId, 
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobId);

        var sanitizedUsername = SanitizeUsername(username);
        var sanitizedBlobId = SanitizeBlobId(blobId);

        var blobPath = Path.Combine(_dataPath, "blobs", sanitizedUsername, sanitizedBlobId);
        var metadataPath = blobPath + ".meta";

        try
        {
            if (File.Exists(blobPath))
            {
                File.Delete(blobPath);
                _logger.LogDebug("Deleted blob {BlobId} for user {Username}", blobId, username);
            }

            if (File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete blob {BlobId} for user {Username}", blobId, username);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> BlobExistsAsync(
        string username, 
        string blobId, 
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobId);

        var sanitizedUsername = SanitizeUsername(username);
        var sanitizedBlobId = SanitizeBlobId(blobId);

        var blobPath = Path.Combine(_dataPath, "blobs", sanitizedUsername, sanitizedBlobId);
        
        await Task.CompletedTask;
        return File.Exists(blobPath);
    }

    /// <inheritdoc/>
    public string BuildBlobUrl(string username, string blobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobId);

        // URL encode the blob ID to handle special characters
        var encodedBlobId = Uri.EscapeDataString(blobId);
        
        return $"{_baseUrl}{_routePrefix}/users/{username}/media/{encodedBlobId}";
    }

    /// <summary>
    /// Sanitizes a username for use in file paths
    /// </summary>
    private static string SanitizeUsername(string username)
    {
        // Remove any path traversal attempts and invalid characters
        var sanitized = username.Replace("..", "")
            .Replace("/", "_")
            .Replace("\\", "_");
        
        return Path.GetFileName(sanitized);
    }

    /// <summary>
    /// Sanitizes a blob ID for use in file paths
    /// </summary>
    private static string SanitizeBlobId(string blobId)
    {
        // Handle blob IDs that might contain path separators (e.g., "folder/file.png")
        // Convert to safe format using hash if necessary
        if (blobId.Contains('/') || blobId.Contains('\\') || blobId.Contains(".."))
        {
            // Use hash for complex paths to avoid directory traversal
            var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(blobId))).ToLowerInvariant();
            var extension = Path.GetExtension(blobId.Split('/').Last());
            return hash + extension;
        }

        // Simple sanitization for simple filenames
        return Path.GetFileName(blobId);
    }

    /// <summary>
    /// Gets content type from file extension
    /// </summary>
    private static string? GetContentTypeFromExtension(string filename)
    {
        var extension = Path.GetExtension(filename);
        if (string.IsNullOrEmpty(extension))
            return null;

        return MimeTypes.TryGetValue(extension, out var mimeType) ? mimeType : null;
    }

    /// <summary>
    /// Metadata stored alongside blobs
    /// </summary>
    private class BlobMetadata
    {
        public string BlobId { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public DateTimeOffset StoredAt { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }
}
