# Quick Start: Activity Delivery Queue

## Automatic Setup

The delivery queue is **automatically enabled** when you call `AddBrocaServer()`. No additional configuration required!

```csharp
var builder = WebApplication.CreateBuilder(args);

// This enables everything including the delivery queue
builder.Services.AddBrocaServer(builder.Configuration);

var app = builder.Build();
app.MapControllers();
app.Run();
```

## What Happens Automatically

1. **Background Worker Starts** - Runs every 5 seconds to process deliveries
2. **Activities Auto-Queue** - When posted to outbox, they're queued for delivery
3. **Deliveries Processed** - Sent to followers' inboxes with HTTP signatures
4. **Retries Handled** - Failed deliveries automatically retry with exponential backoff

## Example Flow

```csharp
// 1. User posts to their outbox
POST /users/alice/outbox
{
  "type": "Create",
  "object": {
    "type": "Note",
    "content": "Hello, world!"
  }
}

// 2. Server automatically:
//    - Saves to alice's outbox
//    - Gets alice's followers list
//    - Queues delivery to each follower's inbox
//    - Background worker delivers activities

// 3. Remote servers receive:
POST https://mastodon.social/users/bob/inbox
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Create",
  "actor": "https://example.com/users/alice",
  "object": {
    "type": "Note",
    "content": "Hello, world!"
  }
}
```

## Configuration Options

### Disable Delivery (if needed)

```json
{
  "ActivityPub": {
    "EnableActivityDelivery": false
  }
}
```

### Monitor Queue (optional)

```csharp
app.MapGet("/admin/delivery-stats", async (IDeliveryQueueRepository queue) =>
{
    var stats = await queue.GetStatisticsAsync();
    return Results.Ok(new
    {
        pending = stats.PendingCount,
        processing = stats.ProcessingCount,
        delivered = stats.DeliveredCount,
        failed = stats.FailedCount,
        dead = stats.DeadCount
    });
});
```

## Custom Database Implementation

For production, replace the in-memory queue with your database:

```csharp
// 1. Implement the interface
public class PostgresDeliveryQueueRepository : IDeliveryQueueRepository
{
    private readonly DbContext _context;
    
    public async Task EnqueueAsync(DeliveryQueueItem item, CancellationToken ct)
    {
        await _context.DeliveryQueue.AddAsync(item, ct);
        await _context.SaveChangesAsync(ct);
    }
    
    // ... implement other methods
}

// 2. Register your implementation
builder.Services.AddSingleton<IDeliveryQueueRepository, PostgresDeliveryQueueRepository>();
```

## That's It!

The delivery queue works out of the box. For advanced usage, see [ACTIVITY_DELIVERY.md](./ACTIVITY_DELIVERY.md).

## Verification

Check logs to confirm the worker is running:

```
[Information] Activity Delivery Worker starting
[Information] Queueing activity abc123 for delivery to 5 followers
[Information] Processing 5 pending deliveries
[Information] Successfully delivered activity to https://mastodon.social/users/bob/inbox
```

## Common Questions

**Q: How often are deliveries processed?**  
A: Every 5 seconds by default.

**Q: What if a delivery fails?**  
A: It automatically retries up to 5 times with exponential backoff (1min, 5min, 15min, 1hr, 4hr).

**Q: What if all retries fail?**  
A: The item is marked as "Dead" and won't be retried. Check logs for errors.

**Q: How do I see failed deliveries?**  
A: Use `GetStatisticsAsync()` or query the repository directly.

**Q: Can I manually retry a failed delivery?**  
A: Yes, update its status back to Pending and it will be picked up again.

**Q: How do I scale this for high volume?**  
A: 
1. Use a database-backed queue
2. Increase batch size
3. Run multiple worker instances
4. Implement shared inbox support (future enhancement)
