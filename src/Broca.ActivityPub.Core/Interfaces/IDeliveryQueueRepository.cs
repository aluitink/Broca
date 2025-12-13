using Broca.ActivityPub.Core.Models;

namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Repository interface for managing activity delivery queue
/// </summary>
public interface IDeliveryQueueRepository
{
    /// <summary>
    /// Enqueues an activity for delivery to a specific inbox
    /// </summary>
    /// <param name="item">Delivery queue item containing activity and target inbox</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EnqueueAsync(DeliveryQueueItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues multiple activities for delivery (batch operation)
    /// </summary>
    /// <param name="items">Collection of delivery queue items</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EnqueueBatchAsync(IEnumerable<DeliveryQueueItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next batch of pending deliveries
    /// </summary>
    /// <param name="batchSize">Maximum number of items to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of pending delivery items</returns>
    Task<IEnumerable<DeliveryQueueItem>> GetPendingDeliveriesAsync(int batchSize = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a delivery as completed successfully
    /// </summary>
    /// <param name="deliveryId">The delivery item ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsDeliveredAsync(string deliveryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a delivery as failed and increments retry count
    /// </summary>
    /// <param name="deliveryId">The delivery item ID</param>
    /// <param name="errorMessage">Error message describing the failure</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsFailedAsync(string deliveryId, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes items from the delivery queue that have exceeded max retries or are too old
    /// </summary>
    /// <param name="maxAge">Maximum age of items to keep</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CleanupOldItemsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets delivery statistics for monitoring
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<DeliveryStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all delivery items for diagnostic purposes (testing/debugging)
    /// </summary>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All delivery queue items</returns>
    Task<IEnumerable<DeliveryQueueItem>> GetAllForDiagnosticsAsync(int maxResults = 100, CancellationToken cancellationToken = default);
}
