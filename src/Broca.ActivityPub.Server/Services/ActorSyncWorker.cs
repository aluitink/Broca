using Broca.ActivityPub.Core.Interfaces;

namespace Broca.ActivityPub.Server.Services;

public class ActorSyncWorker : BackgroundService
{
    private readonly IActorSyncQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ActorSyncWorker> _logger;

    public ActorSyncWorker(
        IActorSyncQueue queue,
        IServiceProvider serviceProvider,
        ILogger<ActorSyncWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Actor Sync Worker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            string actorId;
            try
            {
                actorId = await _queue.ReadAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await SyncActorAsync(actorId, stoppingToken);
        }

        _logger.LogInformation("Actor Sync Worker stopping");
    }

    private async Task SyncActorAsync(string actorId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IRemoteActorSyncService>();
            await syncService.SyncActorAsync(actorId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background actor sync failed for {ActorId}", actorId);
        }
    }
}
