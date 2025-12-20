namespace Broca.ActivityPub.Persistence.EntityFramework.Entities;

/// <summary>
/// Entity for storing follower relationships
/// </summary>
public class FollowerEntity
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FollowerActorId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
