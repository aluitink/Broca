using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Persistence.MySql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Persistence.MySql.MySql;

public class MySqlBlobStorageService : IBlobStorageService
{
    private const string ProviderName = "mysql";

    private readonly IDbContextFactory<BrocaDbContext> _contextFactory;
    private readonly string _baseUrl;

    public MySqlBlobStorageService(
        IDbContextFactory<BrocaDbContext> contextFactory,
        IOptions<MySqlPersistenceOptions> options)
    {
        _contextFactory = contextFactory;
        _baseUrl = options.Value.BaseUrl?.TrimEnd('/') ?? "https://localhost";
    }

    public async Task<string> StoreBlobAsync(string username, string blobId, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        var data = ms.ToArray();

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Blobs.FindAsync([username, blobId], cancellationToken);
        if (existing is null)
        {
            db.Blobs.Add(new BlobEntity
            {
                Username = username,
                BlobId = blobId,
                ContentType = contentType ?? "application/octet-stream",
                StorageProvider = ProviderName,
                Content = data,
                Size = data.LongLength
            });
        }
        else
        {
            existing.ContentType = contentType ?? existing.ContentType;
            existing.StorageProvider = ProviderName;
            existing.StorageKey = null;
            existing.Content = data;
            existing.Size = data.LongLength;
        }
        await db.SaveChangesAsync(cancellationToken);

        return BuildBlobUrl(username, blobId);
    }

    public async Task<(Stream Content, string ContentType)?> GetBlobAsync(string username, string blobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Blobs.FindAsync([username, blobId], cancellationToken);
        if (entity is null)
            return null;

        if (entity.StorageProvider != ProviderName || entity.Content is null)
            return null;

        return (new MemoryStream(entity.Content), entity.ContentType);
    }

    public async Task DeleteBlobAsync(string username, string blobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Blobs
            .Where(b => b.Username == username && b.BlobId == blobId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<bool> BlobExistsAsync(string username, string blobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Blobs.AnyAsync(b => b.Username == username && b.BlobId == blobId, cancellationToken);
    }

    public string BuildBlobUrl(string username, string blobId)
    {
        var encodedBlobId = Uri.EscapeDataString(blobId);
        return $"{_baseUrl}/users/{username}/media/{encodedBlobId}";
    }
}
