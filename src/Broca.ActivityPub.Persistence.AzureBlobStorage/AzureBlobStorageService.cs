using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Broca.ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Persistence.AzureBlobStorage;

/// <summary>
/// Azure Blob Storage implementation of IBlobStorageService
/// </summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly AzureBlobStorageOptions _options;
    private readonly ILogger<AzureBlobStorageService> _logger;
    private bool _containerInitialized;

    public AzureBlobStorageService(
        IOptions<AzureBlobStorageOptions> options,
        ILogger<AzureBlobStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var blobServiceClient = new BlobServiceClient(_options.ConnectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
    }

    public async Task<string> StoreBlobAsync(
        string username,
        string blobId,
        Stream content,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(cancellationToken);

        _logger.LogDebug("Storing blob {BlobId} for user {Username}", blobId, username);

        var blobName = GetBlobName(username, blobId);
        var blobClient = _containerClient.GetBlobClient(blobName);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType ?? "application/octet-stream"
            }
        };

        await blobClient.UploadAsync(content, uploadOptions, cancellationToken);

        return BuildBlobUrl(username, blobId);
    }

    public async Task<(Stream Content, string ContentType)?> GetBlobAsync(
        string username,
        string blobId,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(cancellationToken);

        _logger.LogDebug("Retrieving blob {BlobId} for user {Username}", blobId, username);

        var blobName = GetBlobName(username, blobId);
        var blobClient = _containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            _logger.LogDebug("Blob {BlobId} not found", blobId);
            return null;
        }

        var download = await blobClient.DownloadContentAsync(cancellationToken);
        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

        var contentType = properties.Value.ContentType ?? "application/octet-stream";
        
        // Convert BinaryData to Stream
        var stream = download.Value.Content.ToStream();
        
        return (stream, contentType);
    }

    public async Task DeleteBlobAsync(
        string username,
        string blobId,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(cancellationToken);

        _logger.LogDebug("Deleting blob {BlobId} for user {Username}", blobId, username);

        var blobName = GetBlobName(username, blobId);
        var blobClient = _containerClient.GetBlobClient(blobName);

        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> BlobExistsAsync(
        string username,
        string blobId,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(cancellationToken);

        var blobName = GetBlobName(username, blobId);
        var blobClient = _containerClient.GetBlobClient(blobName);

        return await blobClient.ExistsAsync(cancellationToken);
    }

    public string BuildBlobUrl(string username, string blobId)
    {
        if (!string.IsNullOrEmpty(_options.BaseUrl))
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            return $"{baseUrl}/{_options.ContainerName}/{GetBlobName(username, blobId)}";
        }

        var blobName = GetBlobName(username, blobId);
        var blobClient = _containerClient.GetBlobClient(blobName);
        return blobClient.Uri.ToString();
    }

    private async Task EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        if (_containerInitialized)
            return;

        if (_options.CreateContainerIfNotExists)
        {
            _logger.LogDebug("Ensuring container {ContainerName} exists", _options.ContainerName);

            var publicAccessType = _options.PublicAccess
                ? PublicAccessType.Blob
                : PublicAccessType.None;

            await _containerClient.CreateIfNotExistsAsync(publicAccessType, cancellationToken: cancellationToken);
        }

        _containerInitialized = true;
    }

    private static string GetBlobName(string username, string blobId)
    {
        // Organize blobs by username for better organization
        return $"{username}/{blobId}";
    }
}
