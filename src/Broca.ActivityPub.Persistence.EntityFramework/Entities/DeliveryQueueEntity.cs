namespace Broca.ActivityPub.Persistence.EntityFramework.Entities;

/// <summary>
/// Entity for storing delivery queue items
/// </summary>
public class DeliveryQueueEntity
{
    public long Id { get; set; }
    public string DeliveryId { get; set; } = string.Empty;
    public string InboxUrl { get; set; } = string.Empty;
    public string SenderActorId { get; set; } = string.Empty;
    public string SenderUsername { get; set; } = string.Empty;
    public string ActivityJson { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public int MaxRetries { get; set; } = 5;
    public DateTime CreatedAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Pending";
    public string? LastError { get; set; }
}
