using System.Threading.Channels;
using Broca.ActivityPub.Core.Interfaces;

namespace Broca.ActivityPub.Server.Services;

public class ActorSyncQueue : IActorSyncQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });

    public void Enqueue(string actorId) => _channel.Writer.TryWrite(actorId);

    public Task<string> ReadAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAsync(cancellationToken).AsTask();

    public bool TryRead(out string actorId)
    {
        if (_channel.Reader.TryRead(out var id))
        {
            actorId = id;
            return true;
        }
        actorId = string.Empty;
        return false;
    }
}
