namespace Broca.ActivityPub.Persistence.EntityFramework.Entities;

/// <summary>
/// Entity for storing following relationships
/// </summary>
public class FollowingEntity
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FollowingActorId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
