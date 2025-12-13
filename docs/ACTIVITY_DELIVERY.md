# Activity Delivery Queue System

## Overview

The Broca ActivityPub server includes a built-in activity delivery queue system that automatically delivers activities posted to the outbox to all followers' inboxes. This implements the server-to-server federation aspect of the ActivityPub protocol.

## Architecture

The delivery system consists of four main components:

### 1. **DeliveryQueueRepository** (`IDeliveryQueueRepository`)
Stores pending deliveries and tracks their status. The in-memory implementation is provided by default, but you can implement your own using a database for production use.

### 2. **ActivityDeliveryService**
Handles the logic for:
- Queuing activities for delivery to followers
- Fetching follower inboxes
- Delivering activities with HTTP signatures
- Handling delivery failures and retries

### 3. **ActivityDeliveryWorker**
Background service (HostedService) that:
- Runs continuously in the background
- Processes pending deliveries every 5 seconds
- Cleans up old delivered/failed items every hour
- Automatically starts with the application

### 4. **OutboxProcessor** (Enhanced)
When an activity is posted to the outbox, it automatically queues the activity for delivery to all followers.

## How It Works

### 1. Activity Posted to Outbox

```csharp
POST /users/alice/outbox
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Create",
  "actor": "https://example.com/users/alice",
  "object": {
    "type": "Note",
    "content": "Hello, fediverse!"
  }
}
```

### 2. Automatic Queueing

The `OutboxProcessor` automatically:
1. Saves the activity to alice's outbox
2. Fetches alice's followers list
3. For each follower, fetches their actor to get inbox URL
4. Creates a `DeliveryQueueItem` for each inbox
5. Enqueues all deliveries in batch

### 3. Background Processing

The `ActivityDeliveryWorker` background service:
1. Wakes up every 5 seconds
2. Fetches pending deliveries from the queue
3. Processes up to 100 deliveries in parallel (max 10 concurrent)
4. For each delivery:
   - Fetches sender's private key
   - Signs the HTTP request with HTTP Signatures
   - POSTs the activity to the recipient's inbox
   - Marks as delivered or failed

### 4. Retry Logic with Exponential Backoff

If a delivery fails, it's automatically retried with exponential backoff:

| Attempt | Delay     |
|---------|-----------|
| 1       | 1 minute  |
| 2       | 5 minutes |
| 3       | 15 minutes|
| 4       | 1 hour    |
| 5       | 4 hours   |

After 5 failed attempts, the item is marked as "Dead" and won't be retried.

## Delivery Status Flow

```
Pending → Processing → Delivered ✓
    ↓
  Failed → Retry (with backoff)
    ↓
  Dead (after max retries) ✗
```

## Configuration

### Enable/Disable Delivery

In `appsettings.json`:

```json
{
  "ActivityPub": {
    "EnableActivityDelivery": true
  }
}
```

Set to `false` to disable automatic delivery (activities will still be saved to outbox).

### Customizing Retry Behavior

You can customize retry behavior when creating delivery items:

```csharp
var deliveryItem = new DeliveryQueueItem
{
    Activity = activity,
    InboxUrl = targetInbox,
    SenderActorId = senderActorId,
    SenderUsername = username,
    MaxRetries = 3  // Default is 5
};
```

## Monitoring Delivery Queue

### Get Statistics

```csharp
var deliveryQueue = serviceProvider.GetRequiredService<IDeliveryQueueRepository>();
var stats = await deliveryQueue.GetStatisticsAsync();

Console.WriteLine($"Pending: {stats.PendingCount}");
Console.WriteLine($"Processing: {stats.ProcessingCount}");
Console.WriteLine($"Delivered: {stats.DeliveredCount}");
Console.WriteLine($"Failed: {stats.FailedCount}");
Console.WriteLine($"Dead: {stats.DeadCount}");
Console.WriteLine($"Oldest pending: {stats.OldestPendingItem}");
```

## Production Considerations

### Database-Backed Queue

For production, implement `IDeliveryQueueRepository` with a database backend:

```csharp
public class PostgresDeliveryQueueRepository : IDeliveryQueueRepository
{
    // Use PostgreSQL with proper indexing
    // Index on: Status, NextAttemptAt, CreatedAt
}

// Register in DI
services.AddSingleton<IDeliveryQueueRepository, PostgresDeliveryQueueRepository>();
```

### Monitoring and Alerting

Monitor these metrics:
- **Queue depth** (pending count) - Alert if > 10,000
- **Dead letter count** - Investigate delivery failures
- **Oldest pending item age** - Alert if > 1 hour
- **Delivery success rate** - Should be > 95%

### Scaling Considerations

**For high-volume servers:**

1. **Increase worker count**: Run multiple worker instances
2. **Batch processing**: Increase batch size from 100 to 1000
3. **Shared inbox optimization**: Deduplicate deliveries to the same server
4. **Redis queue**: Use distributed queue for multiple server instances
5. **Dedicated delivery workers**: Run workers on separate servers

### Shared Inbox Support (Future Enhancement)

Many ActivityPub servers provide a shared inbox for efficiency:

```json
{
  "type": "Person",
  "inbox": "https://mastodon.social/users/alice/inbox",
  "sharedInbox": "https://mastodon.social/inbox"  // ← Preferred
}
```

Instead of sending to 1000 individual inboxes on mastodon.social, send once to the shared inbox.

## Error Handling

### Common Delivery Failures

1. **Recipient server down** (503, timeout)
   - Automatically retried with backoff
   
2. **Invalid signature** (401)
   - Check sender's private key
   - Verify key matches actor's public key
   
3. **Recipient not found** (404)
   - Follower may have deleted account
   - Consider removing from followers list
   
4. **Rate limited** (429)
   - Backoff prevents overwhelming recipient
   
5. **Blocked** (403)
   - Recipient server has blocked your server
   - Marked as dead after retries

## Manual Delivery

You can manually queue activities for delivery:

```csharp
var deliveryService = serviceProvider.GetRequiredService<ActivityDeliveryService>();

await deliveryService.QueueActivityForDeliveryAsync(
    senderUsername: "alice",
    activityId: "https://example.com/users/alice/activities/123",
    activity: myActivity
);
```

## Cleanup

Old items are automatically cleaned up:
- **Delivered items**: Removed after 7 days
- **Dead items**: Removed after 7 days

You can manually trigger cleanup:

```csharp
await deliveryQueue.CleanupOldItemsAsync(TimeSpan.FromDays(7));
```

## Implementation Notes

### HTTP Signatures

All deliveries are signed with the sender's private key using HTTP Signatures. This ensures:
- Recipient can verify the activity came from the claimed sender
- Protection against man-in-the-middle attacks
- Compliance with ActivityPub security requirements

### Concurrency

The delivery worker processes up to 10 deliveries concurrently using `SemaphoreSlim`. This provides good throughput while preventing resource exhaustion.

### Idempotency

ActivityPub activities should be idempotent. If the same activity is delivered twice, the recipient should handle it gracefully. The delivery system doesn't prevent duplicate deliveries if the same activity is posted to the outbox multiple times.

## Testing

Run the delivery system tests:

```bash
dotnet test --filter "ActivityDeliveryTests"
```

## Future Enhancements

Planned improvements:
- [ ] Shared inbox support for efficiency
- [ ] Delivery priority queue (urgent vs. normal)
- [ ] Delivery webhooks for monitoring
- [ ] Metrics export (Prometheus)
- [ ] Configurable worker interval
- [ ] Redis-backed distributed queue
- [ ] Delivery rate limiting per domain
- [ ] Automatic follower cleanup on permanent failures

## Troubleshooting

### Deliveries Not Processing

1. **Check if worker is running**:
   - Look for "Activity Delivery Worker starting" in logs
   
2. **Check delivery enabled**:
   - Verify `EnableActivityDelivery: true` in config
   
3. **Check queue statistics**:
   - Use `GetStatisticsAsync()` to see queue state

### High Dead Letter Count

1. **Review error messages**: Check `LastError` on dead items
2. **Verify HTTP signatures**: Ensure private keys are valid
3. **Check network connectivity**: Can your server reach remote inboxes?
4. **Review follower list**: Remove invalid/deleted followers

## References

- [ActivityPub W3C Specification - Server to Server](https://www.w3.org/TR/activitypub/#server-to-server-interactions)
- [HTTP Signatures](https://datatracker.ietf.org/doc/html/draft-cavage-http-signatures)
- [ActivityPub Federation Best Practices](https://socialhub.activitypub.rocks/t/best-practices-for-reliable-federation/2847)
