namespace Broca.ActivityPub.Persistence.MySql.Entities;

public class ActorEntity
{
    public string Username { get; set; } = string.Empty;
    public string? ActorId { get; set; }
    public string ActorJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
