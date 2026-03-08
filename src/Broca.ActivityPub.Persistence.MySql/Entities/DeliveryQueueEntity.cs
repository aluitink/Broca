using Broca.ActivityPub.Core.Models;

namespace Broca.ActivityPub.Persistence.MySql.Entities;

public class DeliveryQueueEntity
{
    public string Id { get; set; } = string.Empty;
    public string ActivityJson { get; set; } = string.Empty;
    public string InboxUrl { get; set; } = string.Empty;
    public string? TargetActorId { get; set; }
    public string SenderActorId { get; set; } = string.Empty;
    public string SenderUsername { get; set; } = string.Empty;
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public int AttemptCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 5;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastError { get; set; }
}
