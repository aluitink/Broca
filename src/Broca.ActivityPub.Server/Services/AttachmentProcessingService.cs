using Broca.ActivityPub.Core.Interfaces;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Service for processing attachments in ActivityPub objects
/// </summary>
/// <remarks>
/// This service handles downloading remote attachments and rewriting URLs
/// to use the local blob storage, ensuring that activities served from
/// inbox/outbox display attachments from the local server.
/// </remarks>
public class AttachmentProcessingService
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AttachmentProcessingService> _logger;

    public AttachmentProcessingService(
        IBlobStorageService blobStorage,
        IHttpClientFactory httpClientFactory,
        ILogger<AttachmentProcessingService> logger)
    {
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes attachments in an object, downloading remote resources and rewriting URLs
    /// </summary>
    /// <param name="obj">The ActivityPub object containing attachments</param>
    /// <param name="username">The username of the actor owning the object</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ProcessAttachmentsAsync(
        IObject obj,
        string username,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        if (obj.Attachment == null || !obj.Attachment.Any())
        {
            return;
        }

        var processedAttachments = new List<IObjectOrLink>();

        foreach (var attachment in obj.Attachment)
        {
            try
            {
                var processed = await ProcessSingleAttachmentAsync(attachment, username, obj.Id, cancellationToken);
                if (processed != null)
                {
                    processedAttachments.Add(processed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process attachment for user {Username}, continuing with original", username);
                // Keep original attachment on failure
                processedAttachments.Add(attachment);
            }
        }

        obj.Attachment = processedAttachments;
    }

    /// <summary>
    /// Processes images in an object
    /// </summary>
    public async Task ProcessImagesAsync(
        IObject obj,
        string username,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        if (obj.Image == null || !obj.Image.Any())
        {
            return;
        }

        var processedImages = new List<IImageOrLink>();

        foreach (var image in obj.Image)
        {
            try
            {
                if (image is Image img)
                {
                    var processed = await ProcessImageAsync(img, username, obj.Id, cancellationToken);
                    if (processed != null)
                    {
                        processedImages.Add(processed);
                    }
                }
                else
                {
                    processedImages.Add(image);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process image for user {Username}, continuing with original", username);
                processedImages.Add(image);
            }
        }

        obj.Image = processedImages;
    }

    /// <summary>
    /// Rewrites attachment URLs to use local storage URLs if already stored
    /// </summary>
    public async Task RewriteAttachmentUrlsAsync(
        IObject obj,
        string username,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        // Rewrite attachments
        if (obj.Attachment != null && obj.Attachment.Any())
        {
            var rewrittenAttachments = new List<IObjectOrLink>();
            foreach (var attachment in obj.Attachment)
            {
                rewrittenAttachments.Add(await RewriteAttachmentUrlAsync(attachment, username, cancellationToken));
            }
            obj.Attachment = rewrittenAttachments;
        }

        // Rewrite images
        if (obj.Image != null && obj.Image.Any())
        {
            var rewrittenImages = new List<IImageOrLink>();
            foreach (var image in obj.Image)
            {
                if (image is Image img)
                {
                    rewrittenImages.Add(await RewriteImageUrlAsync(img, username, cancellationToken));
                }
                else
                {
                    rewrittenImages.Add(image);
                }
            }
            obj.Image = rewrittenImages;
        }
    }

    private async Task<IObjectOrLink> ProcessSingleAttachmentAsync(
        IObjectOrLink attachment,
        string username,
        string? objectId,
        CancellationToken cancellationToken)
    {
        if (attachment is not Document document)
        {
            return attachment;
        }

        var url = document.Url?.FirstOrDefault();
        if (url?.Href == null)
        {
            return attachment;
        }

        // Check if URL is already local
        var localPrefix = _blobStorage.BuildBlobUrl(username, "").TrimEnd('/');
        if (url.Href.ToString().StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return attachment;
        }

        // Download and store the attachment
        var blobId = GenerateBlobId(url.Href, objectId);
        var httpClient = _httpClientFactory.CreateClient();

        try
        {
            using var response = await httpClient.GetAsync(url.Href, cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? document.MediaType;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var localUrl = await _blobStorage.StoreBlobAsync(username, blobId, stream, contentType, cancellationToken);

            // Create new document with local URL
            var newDocument = new Document
            {
                MediaType = contentType,
                Url = new List<Link> { new Link { Href = new Uri(localUrl) } },
                Name = document.Name,
                Summary = document.Summary
            };

            _logger.LogDebug("Downloaded and stored attachment from {RemoteUrl} to {LocalUrl}", url.Href, localUrl);

            return newDocument;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to download attachment from {Url}", url.Href);
            return attachment;
        }
    }

    private async Task<Image> ProcessImageAsync(
        Image image,
        string username,
        string? objectId,
        CancellationToken cancellationToken)
    {
        var url = image.Url?.FirstOrDefault();
        if (url?.Href == null)
        {
            return image;
        }

        // Check if URL is already local
        var localPrefix = _blobStorage.BuildBlobUrl(username, "").TrimEnd('/');
        if (url.Href.ToString().StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return image;
        }

        // Download and store the image
        var blobId = GenerateBlobId(url.Href, objectId);
        var httpClient = _httpClientFactory.CreateClient();

        try
        {
            using var response = await httpClient.GetAsync(url.Href, cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? image.MediaType;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var localUrl = await _blobStorage.StoreBlobAsync(username, blobId, stream, contentType, cancellationToken);

            // Create new image with local URL
            var newImage = new Image
            {
                MediaType = contentType,
                Url = new List<ILink> { new Link { Href = new Uri(localUrl) } },
                Name = image.Name,
                Summary = image.Summary
            };

            _logger.LogDebug("Downloaded and stored image from {RemoteUrl} to {LocalUrl}", url.Href, localUrl);

            return newImage;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to download image from {Url}", url.Href);
            return image;
        }
    }

    private async Task<IObjectOrLink> RewriteAttachmentUrlAsync(
        IObjectOrLink attachment,
        string username,
        CancellationToken cancellationToken)
    {
        if (attachment is not Document document)
        {
            return attachment;
        }

        var url = document.Url?.FirstOrDefault();
        if (url?.Href == null)
        {
            return attachment;
        }

        // Generate the blob ID and check if it exists locally
        var blobId = GenerateBlobId(url.Href, null);
        var exists = await _blobStorage.BlobExistsAsync(username, blobId, cancellationToken);

        if (exists)
        {
            var localUrl = _blobStorage.BuildBlobUrl(username, blobId);
            var newDocument = new Document
            {
                MediaType = document.MediaType,
                Url = new List<Link> { new Link { Href = new Uri(localUrl) } },
                Name = document.Name,
                Summary = document.Summary
            };
            return newDocument;
        }

        return attachment;
    }

    private async Task<Image> RewriteImageUrlAsync(
        Image image,
        string username,
        CancellationToken cancellationToken)
    {
        var url = image.Url?.FirstOrDefault();
        if (url?.Href == null)
        {
            return image;
        }

        // Generate the blob ID and check if it exists locally
        var blobId = GenerateBlobId(url.Href, null);
        var exists = await _blobStorage.BlobExistsAsync(username, blobId, cancellationToken);

        if (exists)
        {
            var localUrl = _blobStorage.BuildBlobUrl(username, blobId);
            var newImage = new Image
            {
                MediaType = image.MediaType,
                Url = new List<ILink> { new Link { Href = new Uri(localUrl) } },
                Name = image.Name,
                Summary = image.Summary
            };
            return newImage;
        }

        return image;
    }

    /// <summary>
    /// Generates a blob ID from a URL
    /// </summary>
    private static string GenerateBlobId(Uri url, string? objectId)
    {
        // Try to extract a meaningful filename from the URL
        var path = url.AbsolutePath;
        var filename = Path.GetFileName(path);

        if (!string.IsNullOrEmpty(filename) && filename.Contains('.'))
        {
            // Use the filename if it has an extension
            return filename;
        }

        // Otherwise, generate a hash-based filename
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(url.ToString())
            )
        ).ToLowerInvariant()[..16];

        return $"{hash}.bin";
    }
}
