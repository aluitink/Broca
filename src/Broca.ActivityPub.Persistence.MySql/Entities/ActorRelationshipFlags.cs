namespace Broca.ActivityPub.Persistence.MySql.Entities;

[Flags]
public enum ActorRelationshipFlags
{
    None            = 0,
    Following       = 1 << 0,
    Follower        = 1 << 1,
    PendingFollowing = 1 << 2,
    PendingFollower  = 1 << 3,
    Blocked         = 1 << 4,
    Muted           = 1 << 5,
}
