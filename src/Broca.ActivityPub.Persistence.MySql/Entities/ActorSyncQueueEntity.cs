namespace Broca.ActivityPub.Persistence.MySql.Entities;

public class ActorSyncQueueEntity
{
    public long Id { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
}
