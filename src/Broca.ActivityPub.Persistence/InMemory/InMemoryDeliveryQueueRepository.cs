using System.Collections.Concurrent;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;

namespace Broca.ActivityPub.Persistence.InMemory;

/// <summary>
/// In-memory implementation of delivery queue repository for testing and development
/// </summary>
public class InMemoryDeliveryQueueRepository : IDeliveryQueueRepository
{
    private readonly ConcurrentDictionary<string, DeliveryQueueItem> _queue = new();
    private readonly object _lockObject = new();

    public Task EnqueueAsync(DeliveryQueueItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        
        _queue[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task EnqueueBatchAsync(IEnumerable<DeliveryQueueItem> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        
        foreach (var item in items)
        {
            _queue[item.Id] = item;
        }
        
        return Task.CompletedTask;
    }

    public Task<IEnumerable<DeliveryQueueItem>> GetPendingDeliveriesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var pendingItems = _queue.Values
            .Where(item => item.IsReadyForDelivery)
            .OrderBy(item => item.CreatedAt)
            .Take(batchSize)
            .ToList();

        // Mark as processing
        foreach (var item in pendingItems)
        {
            item.Status = DeliveryStatus.Processing;
            item.LastAttemptAt = DateTime.UtcNow;
            item.AttemptCount++;
        }

        return Task.FromResult<IEnumerable<DeliveryQueueItem>>(pendingItems);
    }

    public Task MarkAsDeliveredAsync(string deliveryId, CancellationToken cancellationToken = default)
    {
        if (_queue.TryGetValue(deliveryId, out var item))
        {
            item.Status = DeliveryStatus.Delivered;
            item.CompletedAt = DateTime.UtcNow;
            item.LastError = null;
        }
        
        return Task.CompletedTask;
    }

    public Task MarkAsFailedAsync(string deliveryId, string errorMessage, CancellationToken cancellationToken = default)
    {
        if (_queue.TryGetValue(deliveryId, out var item))
        {
            item.LastError = errorMessage;
            
            if (item.AttemptCount >= item.MaxRetries)
            {
                item.Status = DeliveryStatus.Dead;
            }
            else
            {
                item.Status = DeliveryStatus.Failed;
                
                // Exponential backoff: 1min, 5min, 15min, 1hour, 4hours
                var delayMinutes = item.AttemptCount switch
                {
                    1 => 1,
                    2 => 5,
                    3 => 15,
                    4 => 60,
                    _ => 240
                };
                
                item.NextAttemptAt = DateTime.UtcNow.AddMinutes(delayMinutes);
            }
        }
        
        return Task.CompletedTask;
    }

    public Task CleanupOldItemsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow - maxAge;
        
        var itemsToRemove = _queue.Values
            .Where(item => 
                (item.Status == DeliveryStatus.Delivered && item.CompletedAt < cutoffDate) ||
                (item.Status == DeliveryStatus.Dead && item.LastAttemptAt < cutoffDate))
            .Select(item => item.Id)
            .ToList();

        foreach (var id in itemsToRemove)
        {
            _queue.TryRemove(id, out _);
        }
        
        return Task.CompletedTask;
    }

    public Task<DeliveryStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new DeliveryStatistics
        {
            PendingCount = _queue.Values.Count(i => i.Status == DeliveryStatus.Pending),
            ProcessingCount = _queue.Values.Count(i => i.Status == DeliveryStatus.Processing),
            DeliveredCount = _queue.Values.Count(i => i.Status == DeliveryStatus.Delivered),
            FailedCount = _queue.Values.Count(i => i.Status == DeliveryStatus.Failed),
            DeadCount = _queue.Values.Count(i => i.Status == DeliveryStatus.Dead),
            OldestPendingItem = _queue.Values
                .Where(i => i.Status == DeliveryStatus.Pending)
                .OrderBy(i => i.CreatedAt)
                .FirstOrDefault()?.CreatedAt
        };
        
        return Task.FromResult(stats);
    }

    public Task<IEnumerable<DeliveryQueueItem>> GetAllForDiagnosticsAsync(int maxResults = 100, CancellationToken cancellationToken = default)
    {
        var allItems = _queue.Values
            .OrderByDescending(item => item.CreatedAt)
            .Take(maxResults)
            .ToList();
        
        return Task.FromResult<IEnumerable<DeliveryQueueItem>>(allItems);
    }
}
