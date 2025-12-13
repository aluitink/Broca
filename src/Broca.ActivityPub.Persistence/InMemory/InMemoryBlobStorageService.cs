using Broca.ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Persistence.InMemory;

/// <summary>
/// In-memory implementation of blob storage for testing
/// </summary>
public class InMemoryBlobStorageService : IBlobStorageService
{
    private readonly Dictionary<string, (byte[] Data, string ContentType)> _storage = new();
    private readonly string _baseUrl;

    public InMemoryBlobStorageService(IOptions<Core.Models.ActivityPubServerOptions>? options = null)
    {
        _baseUrl = options?.Value?.BaseUrl ?? "https://localhost";
    }

    public Task<string> StoreBlobAsync(string username, string blobId, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(username, blobId);
        
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        var data = ms.ToArray();
        
        _storage[key] = (data, contentType ?? "application/octet-stream");
        
        return Task.FromResult(BuildBlobUrl(username, blobId));
    }

    public Task<(Stream Content, string ContentType)?> GetBlobAsync(string username, string blobId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(username, blobId);
        
        if (_storage.TryGetValue(key, out var blob))
        {
            var stream = new MemoryStream(blob.Data);
            return Task.FromResult<(Stream Content, string ContentType)?>((stream, blob.ContentType));
        }
        
        return Task.FromResult<(Stream Content, string ContentType)?>(null);
    }

    public Task DeleteBlobAsync(string username, string blobId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(username, blobId);
        _storage.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> BlobExistsAsync(string username, string blobId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(username, blobId);
        return Task.FromResult(_storage.ContainsKey(key));
    }

    public string BuildBlobUrl(string username, string blobId)
    {
        // URL encode the blob ID to handle special characters
        var encodedBlobId = Uri.EscapeDataString(blobId);
        return $"{_baseUrl.TrimEnd('/')}/users/{username}/media/{encodedBlobId}";
    }

    private string BuildKey(string username, string blobId)
    {
        return $"{username}/{blobId}";
    }

    /// <summary>
    /// Clears all stored blobs (useful for testing)
    /// </summary>
    public void Clear()
    {
        _storage.Clear();
    }
}
