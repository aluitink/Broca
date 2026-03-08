namespace Broca.ActivityPub.Persistence.MySql.Entities;

public class ActivityEntity
{
    public long Id { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Box { get; set; } = string.Empty;
    public string ActivityJson { get; set; } = string.Empty;
    public string? ActivityType { get; set; }
    public string? ObjectId { get; set; }
    public string? InReplyTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ActorEntity? Actor { get; set; }
}
