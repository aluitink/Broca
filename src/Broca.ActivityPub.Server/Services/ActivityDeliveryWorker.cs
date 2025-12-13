using Broca.ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Background service that processes the activity delivery queue
/// </summary>
public class ActivityDeliveryWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ActivityDeliveryWorker> _logger;
    private readonly TimeSpan _processingInterval;
    private readonly TimeSpan _cleanupInterval;
    private DateTime _lastCleanup = DateTime.UtcNow;

    public ActivityDeliveryWorker(
        IServiceProvider serviceProvider,
        ILogger<ActivityDeliveryWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _processingInterval = TimeSpan.FromSeconds(5); // Process queue every 5 seconds
        _cleanupInterval = TimeSpan.FromHours(1); // Cleanup old items every hour
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Activity Delivery Worker starting");

        // Wait a few seconds on startup to let the application initialize
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDeliveriesAsync(stoppingToken);
                
                // Perform periodic cleanup
                if (DateTime.UtcNow - _lastCleanup > _cleanupInterval)
                {
                    await CleanupOldItemsAsync(stoppingToken);
                    _lastCleanup = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in activity delivery worker");
            }

            // Wait before next processing cycle
            try
            {
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when stopping
                break;
            }
        }

        _logger.LogInformation("Activity Delivery Worker stopping");
    }

    private async Task ProcessDeliveriesAsync(CancellationToken cancellationToken)
    {
        // Create a scope for scoped services
        using var scope = _serviceProvider.CreateScope();
        var deliveryService = scope.ServiceProvider.GetRequiredService<ActivityDeliveryService>();
        
        await deliveryService.ProcessPendingDeliveriesAsync(batchSize: 100, cancellationToken);
    }

    private async Task CleanupOldItemsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var deliveryQueue = scope.ServiceProvider.GetRequiredService<IDeliveryQueueRepository>();
            
            // Remove delivered items older than 7 days and dead items older than 30 days
            await deliveryQueue.CleanupOldItemsAsync(TimeSpan.FromDays(7), cancellationToken);
            
            _logger.LogInformation("Delivery queue cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during delivery queue cleanup");
        }
    }
}
