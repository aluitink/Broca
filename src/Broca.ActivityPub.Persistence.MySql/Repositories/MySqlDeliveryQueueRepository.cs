using System.Text.Json;
using System.Text.Json.Serialization;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.MySql.Entities;
using KristofferStrube.ActivityStreams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Persistence.MySql.Repositories;

public class MySqlDeliveryQueueRepository : IDeliveryQueueRepository
{
    private readonly IDbContextFactory<BrocaDbContext> _contextFactory;
    private readonly ILogger<MySqlDeliveryQueueRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public MySqlDeliveryQueueRepository(
        IDbContextFactory<BrocaDbContext> contextFactory,
        ILogger<MySqlDeliveryQueueRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    public async Task EnqueueAsync(DeliveryQueueItem item, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.DeliveryQueue.Add(ToEntity(item));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EnqueueBatchAsync(IEnumerable<DeliveryQueueItem> items, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.DeliveryQueue.AddRange(items.Select(ToEntity));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<DeliveryQueueItem>> GetPendingDeliveriesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var entities = await db.DeliveryQueue
            .AsNoTracking()
            .Where(d => d.Status == DeliveryStatus.Pending && (d.NextAttemptAt == null || d.NextAttemptAt <= now))
            .OrderBy(d => d.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        return entities.Select(ToModel);
    }

    public async Task MarkAsDeliveredAsync(string deliveryId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.DeliveryQueue
            .Where(d => d.Id == deliveryId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, DeliveryStatus.Delivered)
                .SetProperty(d => d.CompletedAt, DateTime.UtcNow),
            cancellationToken);
    }

    public async Task MarkAsFailedAsync(string deliveryId, string errorMessage, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.DeliveryQueue.FirstOrDefaultAsync(d => d.Id == deliveryId, cancellationToken);
        if (entity is null) return;

        entity.AttemptCount++;
        entity.LastAttemptAt = DateTime.UtcNow;
        entity.LastError = errorMessage;

        if (entity.AttemptCount >= entity.MaxRetries)
        {
            entity.Status = DeliveryStatus.Dead;
        }
        else
        {
            entity.Status = DeliveryStatus.Pending;
            var delay = TimeSpan.FromMinutes(Math.Pow(2, entity.AttemptCount));
            entity.NextAttemptAt = DateTime.UtcNow.Add(delay);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CleanupOldItemsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var cutoff = DateTime.UtcNow - maxAge;
        await db.DeliveryQueue
            .Where(d => (d.Status == DeliveryStatus.Delivered || d.Status == DeliveryStatus.Dead) && d.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<DeliveryStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var groups = await db.DeliveryQueue
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Oldest = (DateTime?)g.Min(d => d.CreatedAt) })
            .ToListAsync(cancellationToken);

        var stats = new DeliveryStatistics();
        foreach (var g in groups)
        {
            switch (g.Status)
            {
                case DeliveryStatus.Pending:
                    stats.PendingCount = g.Count;
                    stats.OldestPendingItem = g.Oldest;
                    break;
                case DeliveryStatus.Processing:
                    stats.ProcessingCount = g.Count;
                    break;
                case DeliveryStatus.Delivered:
                    stats.DeliveredCount = g.Count;
                    break;
                case DeliveryStatus.Failed:
                    stats.FailedCount = g.Count;
                    break;
                case DeliveryStatus.Dead:
                    stats.DeadCount = g.Count;
                    break;
            }
        }

        return stats;
    }

    public async Task<IEnumerable<DeliveryQueueItem>> GetAllForDiagnosticsAsync(int maxResults = 100, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.DeliveryQueue
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .Take(maxResults)
            .ToListAsync(cancellationToken);
        return entities.Select(ToModel);
    }

    private DeliveryQueueEntity ToEntity(DeliveryQueueItem item) => new()
    {
        Id = item.Id,
        ActivityJson = JsonSerializer.Serialize(item.Activity, typeof(IObjectOrLink), _jsonOptions),
        InboxUrl = item.InboxUrl,
        TargetActorId = item.TargetActorId,
        SenderActorId = item.SenderActorId,
        SenderUsername = item.SenderUsername,
        Status = item.Status,
        AttemptCount = item.AttemptCount,
        MaxRetries = item.MaxRetries,
        CreatedAt = item.CreatedAt,
        NextAttemptAt = item.NextAttemptAt,
        LastAttemptAt = item.LastAttemptAt,
        CompletedAt = item.CompletedAt,
        LastError = item.LastError,
    };

    private DeliveryQueueItem ToModel(DeliveryQueueEntity e)
    {
        IObjectOrLink activity;
        try
        {
            activity = JsonSerializer.Deserialize<IObjectOrLink>(e.ActivityJson, _jsonOptions)
                ?? throw new InvalidOperationException("Null activity deserialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize delivery queue activity {Id}", e.Id);
            activity = new ObjectOrLink();
        }

        return new DeliveryQueueItem
        {
            Id = e.Id,
            Activity = activity,
            InboxUrl = e.InboxUrl,
            TargetActorId = e.TargetActorId,
            SenderActorId = e.SenderActorId,
            SenderUsername = e.SenderUsername,
            Status = e.Status,
            AttemptCount = e.AttemptCount,
            MaxRetries = e.MaxRetries,
            CreatedAt = e.CreatedAt,
            NextAttemptAt = e.NextAttemptAt,
            LastAttemptAt = e.LastAttemptAt,
            CompletedAt = e.CompletedAt,
            LastError = e.LastError,
        };
    }
}
