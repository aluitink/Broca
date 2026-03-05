namespace Broca.ActivityPub.Core.Models;

public class RemoteActorSyncOptions
{
    public int OutboxPageLimit { get; set; } = 3;
    public bool SyncFollowers { get; set; } = false;
    public bool SyncFollowing { get; set; } = false;
    public bool SyncMedia { get; set; } = true;
    public TimeSpan MinSyncInterval { get; set; } = TimeSpan.FromHours(24);
}
