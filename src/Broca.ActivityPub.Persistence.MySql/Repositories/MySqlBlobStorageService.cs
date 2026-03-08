using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.MySql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Persistence.MySql.Repositories;

public class MySqlBlobStorageService : IBlobStorageService
{
    private readonly IDbContextFactory<BrocaDbContext> _contextFactory;
    private readonly ILogger<MySqlBlobStorageService> _logger;
    private readonly string _baseUrl;
    private readonly string _routePrefix;

    public MySqlBlobStorageService(
        IDbContextFactory<BrocaDbContext> contextFactory,
        IOptions<ActivityPubServerOptions> serverOptions,
        ILogger<MySqlBlobStorageService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _baseUrl = serverOptions.Value.BaseUrl.TrimEnd('/');
        _routePrefix = serverOptions.Value.NormalizedRoutePrefix;
    }

    public async Task<string> StoreBlobAsync(string username, string blobId, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var actor = await db.Actors.FirstAsync(a => a.Username == key, cancellationToken);

        var existing = await db.Blobs
            .FirstOrDefaultAsync(b => b.ActorId == actor.Id && b.BlobId == blobId, cancellationToken);

        if (existing is null)
        {
            db.Blobs.Add(new BlobEntity
            {
                ActorId = actor.Id,
                BlobId = blobId,
                Content = bytes,
                ContentType = contentType ?? "application/octet-stream",
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Content = bytes;
            existing.ContentType = contentType ?? existing.ContentType;
        }

        await db.SaveChangesAsync(cancellationToken);
        return BuildBlobUrl(username, blobId);
    }

    public async Task<(Stream Content, string ContentType)?> GetBlobAsync(string username, string blobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        var entity = await db.Blobs
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Actor.Username == key && b.BlobId == blobId, cancellationToken);

        if (entity is null) return null;
        return (new MemoryStream(entity.Content), entity.ContentType);
    }

    public async Task DeleteBlobAsync(string username, string blobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        await db.Blobs
            .Where(b => b.Actor.Username == key && b.BlobId == blobId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<bool> BlobExistsAsync(string username, string blobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var key = username.ToLowerInvariant();
        return await db.Blobs
            .AnyAsync(b => b.Actor.Username == key && b.BlobId == blobId, cancellationToken);
    }

    public string BuildBlobUrl(string username, string blobId)
    {
        return $"{_baseUrl}{_routePrefix}/users/{username}/blobs/{Uri.EscapeDataString(blobId)}";
    }
}
