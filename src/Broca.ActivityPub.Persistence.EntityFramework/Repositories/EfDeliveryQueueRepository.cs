using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.EntityFramework.Entities;
using KristofferStrube.ActivityStreams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Broca.ActivityPub.Persistence.EntityFramework.Repositories;

/// <summary>
/// Entity Framework implementation of IDeliveryQueueRepository
/// </summary>
public class EfDeliveryQueueRepository : IDeliveryQueueRepository
{
    private readonly ActivityPubDbContext _context;
    private readonly ILogger<EfDeliveryQueueRepository> _logger;

    public EfDeliveryQueueRepository(ActivityPubDbContext context, ILogger<EfDeliveryQueueRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnqueueAsync(DeliveryQueueItem item, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Enqueueing activity {ActivityId} for delivery to {InboxUrl}", item.Id, item.InboxUrl);
        
        var entity = new DeliveryQueueEntity
        {
            DeliveryId = item.Id,
            InboxUrl = item.InboxUrl,
            SenderActorId = item.SenderActorId,
            SenderUsername = item.SenderUsername,
            ActivityJson = SerializeActivity(item.Activity),
            AttemptCount = item.AttemptCount,
            MaxRetries = item.MaxRetries,
            CreatedAt = item.CreatedAt,
            NextAttemptAt = item.NextAttemptAt ?? DateTime.UtcNow,
            LastAttemptAt = item.LastAttemptAt,
            CompletedAt = item.CompletedAt,
            Status = item.Status.ToString(),
            LastError = item.LastError
        };

        _context.DeliveryQueue.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task EnqueueBatchAsync(IEnumerable<DeliveryQueueItem> items, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Batch enqueueing {Count} delivery items", items.Count());
        
        var entities = items.Select(item => new DeliveryQueueEntity
        {
            DeliveryId = item.Id,
            InboxUrl = item.InboxUrl,
            SenderActorId = item.SenderActorId,
            SenderUsername = item.SenderUsername,
            ActivityJson = SerializeActivity(item.Activity),
            AttemptCount = item.AttemptCount,
            MaxRetries = item.MaxRetries,
            CreatedAt = item.CreatedAt,
            NextAttemptAt = item.NextAttemptAt ?? DateTime.UtcNow,
            LastAttemptAt = item.LastAttemptAt,
            CompletedAt = item.CompletedAt,
            Status = item.Status.ToString(),
            LastError = item.LastError
        }).ToList();

        _context.DeliveryQueue.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<DeliveryQueueItem>> GetPendingDeliveriesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting pending deliveries (batch size: {BatchSize})", batchSize);
        
        var now = DateTime.UtcNow;
        var entities = await _context.DeliveryQueue
            .Where(d => d.Status == "Pending" && (d.NextAttemptAt == null || d.NextAttemptAt <= now))
            .OrderBy(d => d.NextAttemptAt ?? d.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return entities.Select(EntityToItem).Where(item => item != null).Cast<DeliveryQueueItem>().ToList();
    }

    public async Task MarkAsDeliveredAsync(string deliveryId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Marking delivery {DeliveryId} as delivered", deliveryId);
        
        var entity = await _context.DeliveryQueue
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, cancellationToken);
        
        if (entity != null)
        {
            entity.Status = "Delivered";
            entity.CompletedAt = DateTime.UtcNow;
            entity.LastError = null;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsFailedAsync(string deliveryId, string errorMessage, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Marking delivery {DeliveryId} as failed: {Error}", deliveryId, errorMessage);
        
        var entity = await _context.DeliveryQueue
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, cancellationToken);
        
        if (entity != null)
        {
            entity.AttemptCount++;
            entity.LastError = errorMessage;
            entity.LastAttemptAt = DateTime.UtcNow;
            
            // Exponential backoff: 1min, 5min, 15min, 1hr, 4hr, 12hr
            var delays = new[] { 1, 5, 15, 60, 240, 720 };
            var delayMinutes = entity.AttemptCount < delays.Length 
                ? delays[entity.AttemptCount] 
                : 720;
            
            entity.NextAttemptAt = DateTime.UtcNow.AddMinutes(delayMinutes);
            
            // If exceeded max retries, mark as Dead
            if (entity.AttemptCount >= entity.MaxRetries)
            {
                entity.Status = "Dead";
            }
            else
            {
                entity.Status = "Failed";
            }
            
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task CleanupOldItemsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Cleaning up delivery items older than {MaxAge}", maxAge);
        
        var cutoffDate = DateTime.UtcNow - maxAge;
        
        var oldItems = await _context.DeliveryQueue
            .Where(d => (d.Status == "Delivered" || d.Status == "Dead") && d.CreatedAt < cutoffDate)
            .ToListAsync(cancellationToken);

        _context.DeliveryQueue.RemoveRange(oldItems);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Cleaned up {Count} old delivery items", oldItems.Count);
    }

    public async Task<DeliveryStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new DeliveryStatistics
        {
            PendingCount = await _context.DeliveryQueue.CountAsync(d => d.Status == "Pending", cancellationToken),
            ProcessingCount = await _context.DeliveryQueue.CountAsync(d => d.Status == "Processing", cancellationToken),
            DeliveredCount = await _context.DeliveryQueue.CountAsync(d => d.Status == "Delivered", cancellationToken),
            FailedCount = await _context.DeliveryQueue.CountAsync(d => d.Status == "Failed", cancellationToken),
            DeadCount = await _context.DeliveryQueue.CountAsync(d => d.Status == "Dead", cancellationToken),
            OldestPendingItem = await _context.DeliveryQueue
                .Where(d => d.Status == "Pending")
                .OrderBy(d => d.CreatedAt)
                .Select(d => (DateTime?)d.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
        };
        
        return stats;
    }

    public async Task<IEnumerable<DeliveryQueueItem>> GetAllForDiagnosticsAsync(int maxResults = 100, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all delivery items for diagnostics (max: {MaxResults})", maxResults);
        
        var entities = await _context.DeliveryQueue
            .OrderByDescending(d => d.CreatedAt)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return entities.Select(EntityToItem).Where(item => item != null).Cast<DeliveryQueueItem>().ToList();
    }

    private static string SerializeActivity(IObjectOrLink activity)
    {
        // TODO: Implement proper ActivityStreams serialization
        return JsonSerializer.Serialize(activity);
    }

    private static IObjectOrLink? DeserializeActivity(string json)
    {
        // TODO: Implement proper ActivityStreams deserialization
        return JsonSerializer.Deserialize<IObjectOrLink>(json);
    }

    private static DeliveryQueueItem? EntityToItem(DeliveryQueueEntity entity)
    {
        var activity = DeserializeActivity(entity.ActivityJson);
        if (activity == null)
            return null;

        return new DeliveryQueueItem
        {
            Id = entity.DeliveryId,
            Activity = activity,
            InboxUrl = entity.InboxUrl,
            SenderActorId = entity.SenderActorId,
            SenderUsername = entity.SenderUsername,
            Status = Enum.Parse<DeliveryStatus>(entity.Status, ignoreCase: true),
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
