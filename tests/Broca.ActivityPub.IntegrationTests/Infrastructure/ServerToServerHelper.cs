using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using KristofferStrube.ActivityStreams;
using System.Linq;

namespace Broca.ActivityPub.IntegrationTests.Infrastructure;

/// <summary>
/// Helper for Server-to-Server (S2S) ActivityPub interactions and delivery verification
/// Provides polling mechanisms to wait for background delivery to complete
/// </summary>
public class ServerToServerHelper
{
    private readonly BrocaTestServer _server;
    private readonly BrocaTestServer? _sendingServer;
    private readonly TimeSpan _defaultTimeout;
    private readonly TimeSpan _pollInterval;

    public ServerToServerHelper(
        BrocaTestServer server, 
        TimeSpan? defaultTimeout = null, 
        TimeSpan? pollInterval = null,
        BrocaTestServer? sendingServer = null)
    {
        _server = server;
        _sendingServer = sendingServer;
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(10);
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(100);
    }

    /// <summary>
    /// Manually triggers delivery processing on this server
    /// Useful in tests to avoid waiting for the background worker
    /// </summary>
    public async Task ProcessPendingDeliveriesAsync()
    {
        using var scope = _server.Services.CreateScope();
        var deliveryService = scope.ServiceProvider.GetRequiredService<ActivityDeliveryService>();
        await deliveryService.ProcessPendingDeliveriesAsync();
    }

    /// <summary>
    /// Gets the delivery queue statistics for debugging
    /// </summary>
    public async Task<(int Pending, int Processing, int Delivered, int Failed)> GetDeliveryQueueStatsAsync()
    {
        using var scope = _server.Services.CreateScope();
        var deliveryQueue = scope.ServiceProvider.GetRequiredService<IDeliveryQueueRepository>();
        
        var stats = await deliveryQueue.GetStatisticsAsync();
        return (stats.PendingCount, stats.ProcessingCount, stats.DeliveredCount, stats.FailedCount);
    }

    /// <summary>
    /// Gets all pending delivery queue items for debugging
    /// </summary>
    public async Task<IEnumerable<Core.Models.DeliveryQueueItem>> GetPendingDeliveriesAsync()
    {
        using var scope = _server.Services.CreateScope();
        var deliveryQueue = scope.ServiceProvider.GetRequiredService<IDeliveryQueueRepository>();
        
        return await deliveryQueue.GetPendingDeliveriesAsync(100);
    }

    /// <summary>
    /// Polls the inbox until an activity matching the predicate is found
    /// </summary>
    /// <param name="username">The username whose inbox to check</param>
    /// <param name="predicate">Predicate to match the activity</param>
    /// <param name="timeout">Optional timeout (uses default if not specified)</param>
    /// <returns>The matched activity</returns>
    public async Task<Activity> WaitForInboxActivityAsync(
        string username, 
        Func<Activity, bool> predicate,
        TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? _defaultTimeout;
        var endTime = DateTime.UtcNow + actualTimeout;

        while (DateTime.UtcNow < endTime)
        {
            using var scope = _server.Services.CreateScope();
            var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

            var inboxActivities = await activityRepo.GetInboxActivitiesAsync(username, 100);
            
            // Cast to Activity and filter
            var match = inboxActivities.OfType<Activity>().FirstOrDefault(predicate);
            if (match != null)
            {
                return match;
            }

            await Task.Delay(_pollInterval);
        }

        // Collect diagnostic information on timeout
        var diagnostics = await CollectDiagnosticsAsync(username);
        var crossServerDiagnostics = _sendingServer != null 
            ? await CollectCrossServerDiagnosticsAsync(_sendingServer) 
            : string.Empty;
        
        throw new TimeoutException(
            $"Activity not found in {username}'s inbox within {actualTimeout.TotalSeconds} seconds\n" +
            $"Diagnostic Information:\n{diagnostics}{crossServerDiagnostics}");
    }

    /// <summary>
    /// Polls the inbox until an activity with the specified ID is found
    /// </summary>
    public async Task<Activity> WaitForInboxActivityByIdAsync(
        string username, 
        string activityId,
        TimeSpan? timeout = null)
    {
        return await WaitForInboxActivityAsync(
            username, 
            activity => activity.Id == activityId,
            timeout);
    }

    /// <summary>
    /// Polls the inbox until an activity of the specified type is found
    /// </summary>
    public async Task<Activity> WaitForInboxActivityByTypeAsync(
        string username, 
        string activityType,
        TimeSpan? timeout = null)
    {
        return await WaitForInboxActivityAsync(
            username,
            activity => activity.Type?.Contains(activityType) == true,
            timeout);
    }

    /// <summary>
    /// Verifies that an activity was delivered to a user's inbox
    /// </summary>
    public async Task<bool> VerifyDeliveryAsync(
        string username, 
        string activityId,
        TimeSpan? timeout = null)
    {
        try
        {
            await WaitForInboxActivityByIdAsync(username, activityId, timeout);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets all activities from a user's inbox
    /// </summary>
    public async Task<IEnumerable<IObjectOrLink>> GetInboxActivitiesAsync(string username, int limit = 100)
    {
        using var scope = _server.Services.CreateScope();
        var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
        return await activityRepo.GetInboxActivitiesAsync(username, limit);
    }

    /// <summary>
    /// Gets all activities from a user's outbox
    /// </summary>
    public async Task<IEnumerable<IObjectOrLink>> GetOutboxActivitiesAsync(string username, int limit = 100)
    {
        using var scope = _server.Services.CreateScope();
        var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
        return await activityRepo.GetOutboxActivitiesAsync(username, limit);
    }

    /// <summary>
    /// Checks if an activity exists in the activity repository
    /// </summary>
    public async Task<bool> ActivityExistsAsync(string activityId)
    {
        using var scope = _server.Services.CreateScope();
        var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
        
        var activity = await activityRepo.GetActivityByIdAsync(activityId);
        return activity != null;
    }

    /// <summary>
    /// Polls until an activity with the specified ID exists in the repository
    /// </summary>
    public async Task<IObjectOrLink> WaitForActivityAsync(
        string activityId,
        TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? _defaultTimeout;
        var endTime = DateTime.UtcNow + actualTimeout;

        while (DateTime.UtcNow < endTime)
        {
            using var scope = _server.Services.CreateScope();
            var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

            var activity = await activityRepo.GetActivityByIdAsync(activityId);
            if (activity != null)
            {
                return activity;
            }

            await Task.Delay(_pollInterval);
        }

        // Collect diagnostic information on timeout
        var diagnostics = await CollectDiagnosticsAsync(activityId: activityId);
        
        throw new TimeoutException(
            $"Activity {activityId} not found within {actualTimeout.TotalSeconds} seconds\n" +
            $"Diagnostic Information:\n{diagnostics}");
    }

    /// <summary>
    /// Collects diagnostic information about the current state for troubleshooting
    /// </summary>
    private async Task<string> CollectDiagnosticsAsync(string? username = null, string? activityId = null)
    {
        try
        {
            using var scope = _server.Services.CreateScope();
            var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
            var deliveryQueue = scope.ServiceProvider.GetRequiredService<IDeliveryQueueRepository>();

            var diagnosticInfo = $"\nServer: {_server.BaseUrl}\n";

            // Get inbox activities if username is provided
            if (!string.IsNullOrEmpty(username))
            {
                var inboxActivities = await activityRepo.GetInboxActivitiesAsync(username, 100);
                var inboxCount = inboxActivities.Count();
                var inboxTypes = inboxActivities
                    .OfType<Activity>()
                    .Select(a => a.Type?.FirstOrDefault() ?? "unknown")
                    .GroupBy(t => t)
                    .Select(g => $"{g.Key}({g.Count()})")
                    .ToList();

                diagnosticInfo += $@"User: {username}

Inbox Summary:
  Total activities: {inboxCount}
  By type: {string.Join(", ", inboxTypes)}

Recent Inbox Activities (first 5):
{string.Join("\n", inboxActivities.OfType<Activity>().Take(5).Select(a =>
{
    var actorId = a.Actor?.FirstOrDefault() switch
    {
        Link link => link.Href?.ToString(),
        IObject obj => obj.Id,
        _ => "unknown"
    };
    return $"  - Type: {a.Type?.FirstOrDefault()}, Id: {a.Id}, Actor: {actorId}";
}))}
";
            }

            // Get delivery queue stats
            var stats = await deliveryQueue.GetStatisticsAsync();
            
            // Get pending deliveries
            var pending = await deliveryQueue.GetPendingDeliveriesAsync(100);
            var pendingList = pending.Select(d => 
            {
                var activityId = d.Activity switch
                {
                    Activity act => act.Id,
                    Link link => link.Href?.ToString(),
                    IObject obj => obj.Id,
                    _ => "unknown"
                };
                return $"  - Activity: {activityId}, To: {d.InboxUrl}, Status: {d.Status}, Attempts: {d.AttemptCount}";
            }).ToList();

            diagnosticInfo += $@"
Delivery Queue Stats:
  Pending: {stats.PendingCount}
  Processing: {stats.ProcessingCount}
  Delivered: {stats.DeliveredCount}
  Failed: {stats.FailedCount}

{(pendingList.Any() ? $"Pending Deliveries:\n{string.Join("\n", pendingList)}" : "No pending deliveries")}
";

            // If looking for a specific activity, add info about it
            if (!string.IsNullOrEmpty(activityId))
            {
                diagnosticInfo += $"\nSearching for Activity ID: {activityId}\n";
                
                // Check if it's in the pending deliveries
                var relatedDeliveries = pending.Where(d => 
                {
                    var id = d.Activity switch
                    {
                        Activity act => act.Id,
                        Link link => link.Href?.ToString(),
                        IObject obj => obj.Id,
                        _ => null
                    };
                    return id == activityId;
                }).ToList();
                
                if (relatedDeliveries.Any())
                {
                    diagnosticInfo += "Found in pending deliveries:\n";
                    foreach (var delivery in relatedDeliveries)
                    {
                        diagnosticInfo += $"  - To: {delivery.InboxUrl}, Status: {delivery.Status}, Attempts: {delivery.AttemptCount}\n";
                    }
                }
            }

            return diagnosticInfo;
        }
        catch (Exception ex)
        {
            return $"Failed to collect diagnostics: {ex.Message}";
        }
    }

    /// <summary>
    /// Collects diagnostic information from the sending server
    /// </summary>
    private async Task<string> CollectCrossServerDiagnosticsAsync(BrocaTestServer sendingServer)
    {
        try
        {
            using var scope = sendingServer.Services.CreateScope();
            var deliveryQueue = scope.ServiceProvider.GetRequiredService<IDeliveryQueueRepository>();

            // Get delivery queue stats from sending server
            var stats = await deliveryQueue.GetStatisticsAsync();
            
            // Get all deliveries (not just pending)
            var allItems = await deliveryQueue.GetAllForDiagnosticsAsync(100);
            var deliveryList = allItems
                .OrderByDescending(d => d.Status == DeliveryStatus.Failed)
                .ThenByDescending(d => d.Status == DeliveryStatus.Pending)
                .ThenByDescending(d => d.CreatedAt)
                .Select(d => 
                {
                    var activityId = d.Activity switch
                    {
                        Activity act => act.Id,
                        Link link => link.Href?.ToString(),
                        IObject obj => obj.Id,
                        _ => "unknown"
                    };
                    var errorInfo = !string.IsNullOrEmpty(d.LastError) ? $", Error: {d.LastError}" : "";
                    return $"  - [{d.Status}] Activity: {activityId}, To: {d.InboxUrl}, Attempts: {d.AttemptCount}{errorInfo}";
                }).ToList();

            var diagnosticInfo = $@"
--- SENDING SERVER ({sendingServer.BaseUrl}) ---

Delivery Queue Stats:
  Pending: {stats.PendingCount}
  Processing: {stats.ProcessingCount}
  Delivered: {stats.DeliveredCount}
  Failed: {stats.FailedCount}

{(deliveryList.Any() ? $"All Deliveries (showing failed first):\n{string.Join("\n", deliveryList)}" : "No deliveries in queue")}
";
            return diagnosticInfo;
        }
        catch (Exception ex)
        {
            return $"\n--- SENDING SERVER DIAGNOSTICS FAILED: {ex.Message} ---\n";
        }
    }
}
