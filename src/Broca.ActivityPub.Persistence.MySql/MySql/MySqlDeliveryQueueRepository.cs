using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.MySql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Persistence.MySql.MySql;

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
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task EnqueueAsync(DeliveryQueueItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.DeliveryQueue.Add(ToEntity(item));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EnqueueBatchAsync(IEnumerable<DeliveryQueueItem> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.DeliveryQueue.AddRange(items.Select(ToEntity));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<DeliveryQueueItem>> GetPendingDeliveriesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var entities = await db.DeliveryQueue
            .Where(d => d.Status == DeliveryStatus.Pending &&
                        (d.NextAttemptAt == null || d.NextAttemptAt <= now))
            .OrderBy(d => d.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var entity in entities)
        {
            entity.Status = DeliveryStatus.Processing;
            entity.LastAttemptAt = now;
            entity.AttemptCount++;
        }
        await db.SaveChangesAsync(cancellationToken);

        return entities.Select(ToModel).ToList();
    }

    public async Task MarkAsDeliveredAsync(string deliveryId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.DeliveryQueue.FindAsync([deliveryId], cancellationToken);
        if (entity is not null)
        {
            entity.Status = DeliveryStatus.Delivered;
            entity.CompletedAt = DateTime.UtcNow;
            entity.LastError = null;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsFailedAsync(string deliveryId, string errorMessage, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.DeliveryQueue.FindAsync([deliveryId], cancellationToken);
        if (entity is not null)
        {
            entity.LastError = errorMessage;
            if (entity.AttemptCount >= entity.MaxRetries)
            {
                entity.Status = DeliveryStatus.Dead;
            }
            else
            {
                entity.Status = DeliveryStatus.Failed;
                var delayMinutes = entity.AttemptCount switch
                {
                    1 => 1,
                    2 => 5,
                    3 => 15,
                    4 => 60,
                    _ => 240
                };
                entity.NextAttemptAt = DateTime.UtcNow.AddMinutes(delayMinutes);
            }
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task CleanupOldItemsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var cutoff = DateTime.UtcNow - maxAge;
        await db.DeliveryQueue
            .Where(d => (d.Status == DeliveryStatus.Delivered && d.CompletedAt < cutoff) ||
                        (d.Status == DeliveryStatus.Dead && d.LastAttemptAt < cutoff))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<DeliveryStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var groups = await db.DeliveryQueue
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var oldest = await db.DeliveryQueue
            .Where(d => d.Status == DeliveryStatus.Pending)
            .OrderBy(d => d.CreatedAt)
            .Select(d => (DateTime?)d.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new DeliveryStatistics
        {
            PendingCount = groups.FirstOrDefault(g => g.Status == DeliveryStatus.Pending)?.Count ?? 0,
            ProcessingCount = groups.FirstOrDefault(g => g.Status == DeliveryStatus.Processing)?.Count ?? 0,
            DeliveredCount = groups.FirstOrDefault(g => g.Status == DeliveryStatus.Delivered)?.Count ?? 0,
            FailedCount = groups.FirstOrDefault(g => g.Status == DeliveryStatus.Failed)?.Count ?? 0,
            DeadCount = groups.FirstOrDefault(g => g.Status == DeliveryStatus.Dead)?.Count ?? 0,
            OldestPendingItem = oldest
        };
    }

    public async Task<IEnumerable<DeliveryQueueItem>> GetAllForDiagnosticsAsync(int maxResults = 100, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.DeliveryQueue
            .OrderByDescending(d => d.CreatedAt)
            .Take(maxResults)
            .ToListAsync(cancellationToken);
        return entities.Select(ToModel).ToList();
    }

    private DeliveryQueueEntity ToEntity(DeliveryQueueItem item) => new()
    {
        Id = item.Id,
        ActivityJson = JsonSerializer.Serialize(item.Activity, typeof(KristofferStrube.ActivityStreams.IObjectOrLink), _jsonOptions),
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
        LastError = item.LastError
    };

    private DeliveryQueueItem ToModel(DeliveryQueueEntity entity)
    {
        var activity = JsonSerializer.Deserialize<KristofferStrube.ActivityStreams.IObjectOrLink>(
            entity.ActivityJson, _jsonOptions)!;
        return new DeliveryQueueItem
        {
            Id = entity.Id,
            Activity = activity,
            InboxUrl = entity.InboxUrl,
            TargetActorId = entity.TargetActorId,
            SenderActorId = entity.SenderActorId,
            SenderUsername = entity.SenderUsername,
            Status = entity.Status,
            AttemptCount = entity.AttemptCount,
            MaxRetries = entity.MaxRetries,
            CreatedAt = entity.CreatedAt,
            NextAttemptAt = entity.NextAttemptAt,
            LastAttemptAt = entity.LastAttemptAt,
            CompletedAt = entity.CompletedAt,
            LastError = entity.LastError
        };
    }
}
