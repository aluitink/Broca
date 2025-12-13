using Broca.ActivityPub.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Broca.ActivityPub.Server.Controllers;

/// <summary>
/// Controller for serving blob/media attachments
/// </summary>
[ApiController]
[Route("users/{username}/media")]
public class MediaController : ControllerBase
{
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<MediaController> _logger;

    public MediaController(
        IBlobStorageService blobStorage,
        ILogger<MediaController> logger)
    {
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves a media blob for a user
    /// </summary>
    /// <param name="username">Username of the actor owning the blob</param>
    /// <param name="blobId">Unique identifier for the blob</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The blob content with appropriate content type</returns>
    [HttpGet("{*blobId}")]
    public async Task<IActionResult> Get(string username, string blobId, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest(new { error = "Username is required" });
            }

            if (string.IsNullOrWhiteSpace(blobId))
            {
                return BadRequest(new { error = "Blob ID is required" });
            }

            // URL decode the blob ID
            blobId = Uri.UnescapeDataString(blobId);

            var result = await _blobStorage.GetBlobAsync(username, blobId, cancellationToken);

            if (result == null)
            {
                _logger.LogDebug("Blob {BlobId} not found for user {Username}", blobId, username);
                return NotFound(new { error = "Media not found" });
            }

            var (stream, contentType) = result.Value;

            _logger.LogDebug("Serving blob {BlobId} for user {Username} with content type {ContentType}", 
                blobId, username, contentType);

            // Set cache headers for better performance
            Response.Headers.CacheControl = "public, max-age=86400"; // 24 hours
            Response.Headers.ETag = $"\"{blobId}\"";

            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving blob {BlobId} for user {Username}", blobId, username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Deletes a media blob for a user (requires authentication - to be implemented)
    /// </summary>
    /// <param name="username">Username of the actor owning the blob</param>
    /// <param name="blobId">Unique identifier for the blob</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{*blobId}")]
    public async Task<IActionResult> Delete(string username, string blobId, CancellationToken cancellationToken)
    {
        try
        {
            // TODO: Add authentication/authorization to ensure only the owner can delete
            
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest(new { error = "Username is required" });
            }

            if (string.IsNullOrWhiteSpace(blobId))
            {
                return BadRequest(new { error = "Blob ID is required" });
            }

            // URL decode the blob ID
            blobId = Uri.UnescapeDataString(blobId);

            var exists = await _blobStorage.BlobExistsAsync(username, blobId, cancellationToken);
            if (!exists)
            {
                return NotFound(new { error = "Media not found" });
            }

            await _blobStorage.DeleteBlobAsync(username, blobId, cancellationToken);

            _logger.LogInformation("Deleted blob {BlobId} for user {Username}", blobId, username);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting blob {BlobId} for user {Username}", blobId, username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Uploads a new media blob for a user (requires authentication - to be implemented)
    /// </summary>
    /// <param name="username">Username of the actor owning the blob</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The URL of the uploaded blob</returns>
    [HttpPost]
    public async Task<IActionResult> Upload(string username, CancellationToken cancellationToken)
    {
        try
        {
            // TODO: Add authentication/authorization to ensure only the owner can upload
            
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest(new { error = "Username is required" });
            }

            if (Request.ContentLength == null || Request.ContentLength == 0)
            {
                return BadRequest(new { error = "No file content provided" });
            }

            // Get content type from header
            var contentType = Request.ContentType ?? "application/octet-stream";

            // Generate a unique blob ID based on timestamp and random component
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var random = Guid.NewGuid().ToString("N")[..8];
            var extension = GetExtensionFromContentType(contentType);
            var blobId = $"{timestamp}_{random}{extension}";

            // Store the blob
            await using var stream = Request.Body;
            var url = await _blobStorage.StoreBlobAsync(username, blobId, stream, contentType, cancellationToken);

            _logger.LogInformation("Uploaded blob {BlobId} for user {Username}", blobId, username);

            return Created(url, new { url, blobId, contentType });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading blob for user {Username}", username);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private static string GetExtensionFromContentType(string contentType)
    {
        var extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "image/jpeg", ".jpg" },
            { "image/jpg", ".jpg" },
            { "image/png", ".png" },
            { "image/gif", ".gif" },
            { "image/webp", ".webp" },
            { "image/svg+xml", ".svg" },
            { "video/mp4", ".mp4" },
            { "video/webm", ".webm" },
            { "audio/mpeg", ".mp3" },
            { "audio/ogg", ".ogg" },
            { "application/pdf", ".pdf" }
        };

        return extensions.TryGetValue(contentType, out var ext) ? ext : ".bin";
    }
}
