using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Core.Models;

/// <summary>
/// Represents an activity queued for delivery to a remote inbox
/// </summary>
public class DeliveryQueueItem
{
    /// <summary>
    /// Unique identifier for this delivery attempt
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The activity to be delivered
    /// </summary>
    public required IObjectOrLink Activity { get; set; }

    /// <summary>
    /// The target inbox URL
    /// </summary>
    public required string InboxUrl { get; set; }

    /// <summary>
    /// The actor sending this activity (for signing requests)
    /// </summary>
    public required string SenderActorId { get; set; }

    /// <summary>
    /// Username of the sender (for key lookup)
    /// </summary>
    public required string SenderUsername { get; set; }

    /// <summary>
    /// Current delivery status
    /// </summary>
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    /// <summary>
    /// Number of delivery attempts made
    /// </summary>
    public int AttemptCount { get; set; } = 0;

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// When the item was added to the queue
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the next delivery attempt should be made
    /// </summary>
    public DateTime? NextAttemptAt { get; set; }

    /// <summary>
    /// When the last delivery attempt was made
    /// </summary>
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>
    /// When the delivery was successfully completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Last error message if delivery failed
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Whether this delivery should be retried
    /// </summary>
    public bool ShouldRetry => Status == DeliveryStatus.Failed && AttemptCount < MaxRetries;

    /// <summary>
    /// Whether this item is ready for delivery
    /// </summary>
    public bool IsReadyForDelivery => 
        Status == DeliveryStatus.Pending && 
        (!NextAttemptAt.HasValue || NextAttemptAt.Value <= DateTime.UtcNow);
}

/// <summary>
/// Status of a delivery attempt
/// </summary>
public enum DeliveryStatus
{
    /// <summary>
    /// Waiting to be delivered
    /// </summary>
    Pending,

    /// <summary>
    /// Currently being processed
    /// </summary>
    Processing,

    /// <summary>
    /// Successfully delivered
    /// </summary>
    Delivered,

    /// <summary>
    /// Failed delivery (will retry if attempts remaining)
    /// </summary>
    Failed,

    /// <summary>
    /// Permanently failed (exceeded max retries)
    /// </summary>
    Dead
}

/// <summary>
/// Statistics about the delivery queue
/// </summary>
public class DeliveryStatistics
{
    public int PendingCount { get; set; }
    public int ProcessingCount { get; set; }
    public int DeliveredCount { get; set; }
    public int FailedCount { get; set; }
    public int DeadCount { get; set; }
    public DateTime? OldestPendingItem { get; set; }
}
