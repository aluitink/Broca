using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Broca.ActivityPub.Core.Models;

namespace Broca.ActivityPub.Persistence.MySql.Entities;

[Table("delivery_queue")]
public class DeliveryQueueEntity
{
    [Key]
    [MaxLength(255)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column(TypeName = "json")]
    public string ActivityJson { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string InboxUrl { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? TargetActorId { get; set; }

    [MaxLength(2048)]
    public string SenderActorId { get; set; } = string.Empty;

    [MaxLength(255)]
    public string SenderUsername { get; set; } = string.Empty;

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    public int AttemptCount { get; set; } = 0;

    public int MaxRetries { get; set; } = 5;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? NextAttemptAt { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    [MaxLength(4096)]
    public string? LastError { get; set; }
}
