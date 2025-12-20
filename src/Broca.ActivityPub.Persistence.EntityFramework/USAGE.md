# Using the Expanded EF Persistence Layer

This guide shows how to use the expanded Entity Framework persistence layer with its helper services.

## Setup

### 1. Register Services

```csharp
using Broca.ActivityPub.Persistence.EntityFramework.Extensions;

// In your Program.cs or Startup.cs
services.AddActivityPubEntityFramework(options =>
{
    // Choose your database provider
    options.UseNpgsql(connectionString);
    // or options.UseSqlServer(connectionString);
    // or options.UseSqlite(connectionString);
});
```

This automatically registers:
- `ActivityPubDbContext`
- `ActivityStreamExtractor` (for extracting normalized fields)
- `CountManager` (for managing denormalized counts)
- All repository implementations

### 2. Apply Migrations

Create a migration for the expanded schema:

```bash
cd src/Broca.ActivityPub.Persistence.EntityFramework
dotnet ef migrations add ExpandedSchema --context ActivityPubDbContext
dotnet ef database update --context ActivityPubDbContext
```

Or apply migrations at runtime:

```csharp
await app.Services.MigrateActivityPubDatabaseAsync();
```

## How It Works

### Activity Storage with Extraction

When you save an activity using `IActivityRepository`:

```csharp
await activityRepository.SaveInboxActivityAsync(username, activityId, activity);
```

The repository automatically:

1. **Extracts normalized fields** from the ActivityStreams JSON:
   - Activity type, actor ID, object ID
   - Threading info (InReplyTo, ConversationId)
   - Content fields (text, HTML, summary)
   - Timestamps, visibility flags

2. **Saves related entities** in separate tables:
   - Recipients (To/Cc/Bcc) → `ActivityRecipientEntity`
   - Attachments (images, videos) → `ActivityAttachmentEntity`
   - Tags (hashtags, mentions) → `ActivityTagEntity`

3. **Updates denormalized counts**:
   - Increments status count for Create activities
   - Increments like count for Like activities
   - Increments share count for Announce activities
   - Increments reply count when InReplyTo is present

### Querying with Normalized Fields

The repository uses the normalized fields for efficient queries:

```csharp
// Get replies - uses InReplyTo index
var replies = await activityRepository.GetRepliesAsync(objectId);

// Get likes - uses ActivityType and ObjectId indexes
var likes = await activityRepository.GetLikesAsync(objectId);

// Get shares - uses ActivityType and ObjectId indexes
var shares = await activityRepository.GetSharesAsync(objectId);
```

### Count Management

Counts are automatically maintained, but you can also manage them manually:

```csharp
// Inject CountManager
public class MyService
{
    private readonly CountManager _countManager;
    
    public MyService(CountManager countManager)
    {
        _countManager = countManager;
    }
    
    // Manual count operations
    await _countManager.IncrementLikeCountAsync(objectId);
    await _countManager.DecrementLikeCountAsync(objectId);
    
    // Recalculate from actual data (for fixing drift)
    await _countManager.RecalculateLikeCountAsync(objectId);
    await _countManager.RecalculateReplyCountAsync(objectId);
    await _countManager.RecalculateShareCountAsync(objectId);
}
```

## Database Schema Benefits

### Integer Primary Keys
All entities use `bigint` auto-increment IDs for:
- Fast joins
- Efficient foreign keys
- Smaller indexes

### ActivityPub ID Indexes
Unique indexes on ActivityId, ObjectId, ActorId enable:
- Fast lookups by AP ID
- Duplicate prevention
- Federation support

### Normalized Recipients
Recipients in separate table allows:
- Efficient "get all activities sent to X" queries
- No JSON parsing
- Proper indexing on recipient addresses

### Example Queries

```sql
-- Get all public activities (uses IsPublic index)
SELECT * FROM Activities WHERE IsPublic = 1 ORDER BY Published DESC;

-- Get activities in a conversation (uses ConversationId index)
SELECT * FROM Activities WHERE ConversationId = '...' ORDER BY Published;

-- Find activities mentioning a user (uses ActivityTags)
SELECT a.* FROM Activities a
JOIN ActivityTags t ON a.Id = t.ActivityId
WHERE t.TagType = 'Mention' AND t.Href = 'https://example.com/users/alice';

-- Get activities with hashtag (uses ActivityTags)
SELECT a.* FROM Activities a
JOIN ActivityTags t ON a.Id = t.ActivityId
WHERE t.TagType = 'Hashtag' AND t.Name = '#activitypub';
```

## Migration from Minimal Schema

If you have existing data in the minimal schema:

1. **Create and apply migration** - new columns will be added with defaults
2. **Run extraction job** to populate normalized fields:

```csharp
public async Task ExtractExistingDataAsync()
{
    var activities = await _context.Activities.ToListAsync();
    
    foreach (var activity in activities)
    {
        var activityStream = JsonSerializer.Deserialize<IObjectOrLink>(activity.ActivityJson);
        if (activityStream != null)
        {
            // Re-extract fields
            _extractor.ExtractActivityFields(activityStream, activity);
            
            // Extract related entities
            if (activityStream is IObject obj)
            {
                var recipients = _extractor.ExtractRecipients(obj, activityId: activity.Id);
                await _context.ActivityRecipients.AddRangeAsync(recipients);
                
                var attachments = _extractor.ExtractAttachments(obj, activityId: activity.Id);
                await _context.ActivityAttachments.AddRangeAsync(attachments);
                
                var tags = _extractor.ExtractTags(obj, activityId: activity.Id);
                await _context.ActivityTags.AddRangeAsync(tags);
            }
        }
    }
    
    await _context.SaveChangesAsync();
}
```

3. **Recalculate counts**:

```csharp
public async Task RecalculateAllCountsAsync()
{
    var activityIds = await _context.Activities
        .Select(a => a.ActivityId)
        .ToListAsync();
    
    foreach (var activityId in activityIds)
    {
        await _countManager.RecalculateReplyCountAsync(activityId);
        await _countManager.RecalculateLikeCountAsync(activityId);
        await _countManager.RecalculateShareCountAsync(activityId);
    }
}
```

## Performance Tips

1. **Use denormalized counts** - GetLikesCountAsync, GetRepliesCountAsync return cached counts first
2. **Query by normalized fields** - avoid JSON searching in WHERE clauses
3. **Use indexes** - queries on ActivityType, IsPublic, ConversationId, InReplyTo are all indexed
4. **Batch operations** - use AddRangeAsync for multiple related entities
5. **Consider read replicas** - offload timeline queries to read-only replicas

## Future Enhancements

- **Blob storage integration** - use `BlobStorageKey` to store raw JSON externally
- **Full-text search** - add FTS indexes on ContentText for search
- **Partitioning** - partition by date or username for massive scale
- **Materialized views** - for complex timeline queries
