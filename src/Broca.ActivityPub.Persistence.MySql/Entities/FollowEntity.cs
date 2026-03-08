namespace Broca.ActivityPub.Persistence.MySql.Entities;

public enum FollowType
{
    Follower,
    Following,
    PendingFollower,
    PendingFollowing
}

public class FollowEntity
{
    public string Username { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public FollowType FollowType { get; set; }
}
