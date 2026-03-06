namespace Broca.ActivityPub.Core.Interfaces;

public interface IActorSyncQueue
{
    void Enqueue(string actorId);
    Task<string> ReadAsync(CancellationToken cancellationToken = default);
    bool TryRead(out string actorId);
}
