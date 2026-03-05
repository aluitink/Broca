using System.Text.Json;
using System.Text.Json.Serialization;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Persistence.FileSystem;

public class FileSystemDeliveryQueueRepository : IDeliveryQueueRepository
{
    private readonly string _queuePath;
    private readonly string _deadLetterPath;
    private readonly string _deliveredPath;
    private readonly FileSystemPersistenceOptions _options;
    private readonly ILogger<FileSystemDeliveryQueueRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public FileSystemDeliveryQueueRepository(
        IOptions<FileSystemPersistenceOptions> options,
        ILogger<FileSystemDeliveryQueueRepository> logger)
    {
        _options = options.Value;
        var dataPath = _options.DataPath ?? throw new ArgumentException("DataPath must be configured.", nameof(options));
        _logger = logger;

        _queuePath = Path.Combine(dataPath, "delivery", "queue");
        _deadLetterPath = Path.Combine(dataPath, "delivery", "dead");
        _deliveredPath = Path.Combine(dataPath, "delivery", "delivered");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        Directory.CreateDirectory(_queuePath);
        Directory.CreateDirectory(_deadLetterPath);
        Directory.CreateDirectory(_deliveredPath);

        RecoverInterruptedDeliveries();
    }

    public async Task EnqueueAsync(DeliveryQueueItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await WriteEnvelopeAsync(GetQueueFilePath(item.Id), item, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task EnqueueBatchAsync(IEnumerable<DeliveryQueueItem> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var item in items)
                await WriteEnvelopeAsync(GetQueueFilePath(item.Id), item, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IEnumerable<DeliveryQueueItem>> GetPendingDeliveriesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var candidates = new List<DeliveryQueueItem>();

            foreach (var file in Directory.GetFiles(_queuePath, "*.json"))
            {
                try
                {
                    var item = await ReadQueueItemAsync(file, cancellationToken);
                    if (item is null) continue;

                    var isPendingReady = item.Status == DeliveryStatus.Pending
                        && (!item.NextAttemptAt.HasValue || item.NextAttemptAt.Value <= now);

                    var isFailedRetryReady = item.Status == DeliveryStatus.Failed
                        && item.AttemptCount < item.MaxRetries
                        && item.NextAttemptAt.HasValue && item.NextAttemptAt.Value <= now;

                    if (isPendingReady || isFailedRetryReady)
                        candidates.Add(item);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read delivery queue item from {File}", file);
                }
            }

            var batch = candidates
                .OrderBy(i => i.CreatedAt)
                .Take(batchSize)
                .ToList();

            foreach (var item in batch)
            {
                item.Status = DeliveryStatus.Processing;
                item.LastAttemptAt = now;
                item.AttemptCount++;
                await WriteEnvelopeAsync(GetQueueFilePath(item.Id), item, cancellationToken);
            }

            return batch;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task MarkAsDeliveredAsync(string deliveryId, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var queueFile = GetQueueFilePath(deliveryId);
            if (!File.Exists(queueFile)) return;

            var item = await ReadQueueItemAsync(queueFile, cancellationToken);
            if (item is null) return;

            item.Status = DeliveryStatus.Delivered;
            item.CompletedAt = DateTime.UtcNow;
            item.LastError = null;

            await WriteEnvelopeAsync(GetDeliveredFilePath(deliveryId), item, cancellationToken);
            File.Delete(queueFile);

            _logger.LogInformation(
                "Delivery {DeliveryId} completed: {ActivityType} from {Sender} to {InboxUrl}{TargetActor} after {Attempts} attempt(s)",
                deliveryId, item.Activity.Type?.FirstOrDefault() ?? "Unknown", item.SenderUsername, item.InboxUrl,
                item.TargetActorId != null ? $" (actor: {item.TargetActorId})" : "", item.AttemptCount);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task MarkAsFailedAsync(string deliveryId, string errorMessage, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var queueFile = GetQueueFilePath(deliveryId);
            if (!File.Exists(queueFile)) return;

            var item = await ReadQueueItemAsync(queueFile, cancellationToken);
            if (item is null) return;

            item.LastError = errorMessage;

            if (item.AttemptCount >= item.MaxRetries)
            {
                item.Status = DeliveryStatus.Dead;
                await WriteEnvelopeAsync(GetDeadLetterFilePath(deliveryId), item, cancellationToken);
                File.Delete(queueFile);
                _logger.LogWarning(
                    "Delivery {DeliveryId} to {InboxUrl} exhausted {MaxRetries} attempts and moved to dead-letter queue. Last error: {Error}",
                    deliveryId, item.InboxUrl, item.MaxRetries, errorMessage);
            }
            else
            {
                item.Status = DeliveryStatus.Failed;
                item.NextAttemptAt = DateTime.UtcNow.AddMinutes(GetRetryDelayMinutes(item.AttemptCount));
                await WriteEnvelopeAsync(queueFile, item, cancellationToken);
                _logger.LogWarning(
                    "Delivery {DeliveryId} to {InboxUrl} failed (attempt {Attempts}/{MaxRetries}), retry scheduled at {NextAttempt}. Error: {Error}",
                    deliveryId, item.InboxUrl, item.AttemptCount, item.MaxRetries, item.NextAttemptAt, errorMessage);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task CleanupOldItemsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var deliveredCutoff = DateTime.UtcNow - maxAge;
            var deadLetterCutoff = DateTime.UtcNow - TimeSpan.FromDays(_options.DeadLetterRetentionDays);
            var removed = 0;

            foreach (var file in Directory.GetFiles(_deliveredPath, "*.json"))
            {
                try
                {
                    var envelope = await ReadEnvelopeAsync(file, cancellationToken);
                    if (envelope?.CompletedAt < deliveredCutoff)
                    {
                        File.Delete(file);
                        removed++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cleaning up delivered item {File}", file);
                }
            }

            foreach (var file in Directory.GetFiles(_deadLetterPath, "*.json"))
            {
                try
                {
                    var envelope = await ReadEnvelopeAsync(file, cancellationToken);
                    if (envelope?.LastAttemptAt < deadLetterCutoff)
                    {
                        File.Delete(file);
                        removed++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cleaning up dead-letter item {File}", file);
                }
            }

            if (removed > 0)
                _logger.LogInformation("Delivery queue cleanup removed {Count} old items", removed);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<DeliveryStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new DeliveryStatistics();
        DateTime? oldestPending = null;

        foreach (var file in Directory.GetFiles(_queuePath, "*.json"))
        {
            try
            {
                var envelope = await ReadEnvelopeAsync(file, cancellationToken);
                if (envelope is null) continue;

                switch (envelope.Status)
                {
                    case DeliveryStatus.Pending:
                        stats.PendingCount++;
                        if (!oldestPending.HasValue || envelope.CreatedAt < oldestPending.Value)
                            oldestPending = envelope.CreatedAt;
                        break;
                    case DeliveryStatus.Processing:
                        stats.ProcessingCount++;
                        break;
                    case DeliveryStatus.Failed:
                        stats.FailedCount++;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading queue file {File} for statistics", file);
            }
        }

        stats.DeliveredCount = Directory.GetFiles(_deliveredPath, "*.json").Length;
        stats.DeadCount = Directory.GetFiles(_deadLetterPath, "*.json").Length;
        stats.OldestPendingItem = oldestPending;

        return stats;
    }

    public async Task<IEnumerable<DeliveryQueueItem>> GetAllForDiagnosticsAsync(int maxResults = 100, CancellationToken cancellationToken = default)
    {
        var allItems = new List<DeliveryQueueItem>();

        foreach (var directory in new[] { _queuePath, _deadLetterPath, _deliveredPath })
        {
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                try
                {
                    var item = await ReadQueueItemAsync(file, cancellationToken);
                    if (item is not null)
                        allItems.Add(item);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading {File} for diagnostics", file);
                }

                if (allItems.Count >= maxResults) break;
            }

            if (allItems.Count >= maxResults) break;
        }

        return allItems
            .OrderByDescending(i => i.CreatedAt)
            .Take(maxResults)
            .ToList();
    }

    private void RecoverInterruptedDeliveries()
    {
        try
        {
            foreach (var file in Directory.GetFiles(_queuePath, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var envelope = JsonSerializer.Deserialize<QueueItemEnvelope>(json, _jsonOptions);
                    if (envelope?.Status != DeliveryStatus.Processing) continue;

                    envelope.Status = DeliveryStatus.Pending;
                    envelope.NextAttemptAt = null;
                    File.WriteAllText(file, JsonSerializer.Serialize(envelope, _jsonOptions));
                    _logger.LogInformation(
                        "Recovered interrupted delivery {DeliveryId} back to Pending state after restart",
                        envelope.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to recover delivery from {File} after restart", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error scanning delivery queue for crash recovery");
        }
    }

    private async Task WriteEnvelopeAsync(string filePath, DeliveryQueueItem item, CancellationToken cancellationToken)
    {
        var activityType = item.Activity.GetType();
        var envelope = new QueueItemEnvelope
        {
            Id = item.Id,
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
            ActivityType = activityType.FullName ?? activityType.Name,
            ActivityData = JsonSerializer.SerializeToElement(item.Activity, activityType, _jsonOptions)
        };

        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(envelope, _jsonOptions), cancellationToken);
    }

    private async Task<DeliveryQueueItem?> ReadQueueItemAsync(string filePath, CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(filePath, cancellationToken);
        if (envelope is null) return null;

        var activityType = ResolveActivityType(envelope.ActivityType);
        if (activityType is null)
        {
            _logger.LogWarning(
                "Cannot resolve activity type '{TypeName}' from {File} — item will be skipped",
                envelope.ActivityType, filePath);
            return null;
        }

        var activity = (IObjectOrLink?)JsonSerializer.Deserialize(envelope.ActivityData, activityType, _jsonOptions);
        if (activity is null) return null;

        return new DeliveryQueueItem
        {
            Id = envelope.Id,
            Activity = activity,
            InboxUrl = envelope.InboxUrl,
            TargetActorId = envelope.TargetActorId,
            SenderActorId = envelope.SenderActorId,
            SenderUsername = envelope.SenderUsername,
            Status = envelope.Status,
            AttemptCount = envelope.AttemptCount,
            MaxRetries = envelope.MaxRetries,
            CreatedAt = envelope.CreatedAt,
            NextAttemptAt = envelope.NextAttemptAt,
            LastAttemptAt = envelope.LastAttemptAt,
            CompletedAt = envelope.CompletedAt,
            LastError = envelope.LastError
        };
    }

    private async Task<QueueItemEnvelope?> ReadEnvelopeAsync(string filePath, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<QueueItemEnvelope>(json, _jsonOptions);
    }

    private static Type? ResolveActivityType(string typeName)
    {
        var assembly = typeof(Activity).Assembly;
        var type = assembly.GetTypes().FirstOrDefault(t => t.FullName == typeName);
        return type ?? Type.GetType(typeName);
    }

    private static int GetRetryDelayMinutes(int attemptCount) => attemptCount switch
    {
        1 => 1,
        2 => 5,
        3 => 15,
        4 => 60,
        _ => 240
    };

    private string GetQueueFilePath(string id) => Path.Combine(_queuePath, $"{id}.json");
    private string GetDeadLetterFilePath(string id) => Path.Combine(_deadLetterPath, $"{id}.json");
    private string GetDeliveredFilePath(string id) => Path.Combine(_deliveredPath, $"{id}.json");

    private sealed class QueueItemEnvelope
    {
        public string Id { get; set; } = "";
        public string InboxUrl { get; set; } = "";
        public string? TargetActorId { get; set; }
        public string SenderActorId { get; set; } = "";
        public string SenderUsername { get; set; } = "";
        public DeliveryStatus Status { get; set; }
        public int AttemptCount { get; set; }
        public int MaxRetries { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? LastError { get; set; }
        public string ActivityType { get; set; } = "";
        public JsonElement ActivityData { get; set; }
    }
}
