using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Persistence.MySql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Persistence.MySql.MySql;

public class MySqlActorSyncQueue : IActorSyncQueue
{
    private const int PollIntervalMs = 2000;

    private readonly IDbContextFactory<BrocaDbContext> _contextFactory;
    private readonly ILogger<MySqlActorSyncQueue> _logger;

    public MySqlActorSyncQueue(
        IDbContextFactory<BrocaDbContext> contextFactory,
        ILogger<MySqlActorSyncQueue> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public void Enqueue(string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return;

        // Fire-and-forget: the unique index on ActorId handles deduplication atomically.
        _ = Task.Run(async () =>
        {
            try
            {
                await using var db = await _contextFactory.CreateDbContextAsync();
                db.ActorSyncQueue.Add(new ActorSyncQueueEntity
                {
                    ActorId = actorId,
                    EnqueuedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Unique constraint violation: actor already queued. This is expected and safe.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enqueue actor sync for {ActorId}", actorId);
            }
        });
    }

    public async Task<string> ReadAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var actorId = await TryDequeueAsync(cancellationToken);
            if (actorId is not null)
                return actorId;

            await Task.Delay(PollIntervalMs, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return string.Empty;
    }

    // NOTE: This method is safe to call only from background threads without an ASP.NET
    // synchronization context, as it blocks synchronously on async code.
    public bool TryRead(out string actorId)
    {
        var result = TryDequeueAsync(CancellationToken.None).GetAwaiter().GetResult();
        if (result is not null)
        {
            actorId = result;
            return true;
        }
        actorId = string.Empty;
        return false;
    }

    private async Task<string?> TryDequeueAsync(CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.ActorSyncQueue
            .OrderBy(a => a.EnqueuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
            return null;

        // Optimistic delete: ExecuteDeleteAsync is atomic. If another consumer claimed this
        // row first, deleted == 0 and we return null to retry on the next poll cycle.
        var deleted = await db.ActorSyncQueue
            .Where(a => a.Id == entity.Id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0 ? entity.ActorId : null;
    }
}
