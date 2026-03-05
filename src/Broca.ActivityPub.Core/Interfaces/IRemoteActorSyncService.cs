using Broca.ActivityPub.Core.Models;

namespace Broca.ActivityPub.Core.Interfaces;

public interface IRemoteActorSyncService
{
    Task SyncActorAsync(string actorId, CancellationToken cancellationToken = default);
    Task SyncActorAsync(string actorId, RemoteActorSyncOptions options, CancellationToken cancellationToken = default);
    Task<DateTimeOffset?> GetLastSyncTimeAsync(string actorId, CancellationToken cancellationToken = default);
}
