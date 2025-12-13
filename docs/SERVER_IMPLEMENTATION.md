# ActivityPub Collection Implementation Summary

## Overview

This implementation adds comprehensive collection support to the Broca ActivityPub server, enabling proper federation and object management according to the ActivityPub specification.

## Key Concepts

### 1. **Unique Object URIs**
Every ActivityPub object must have a unique, dereferenceable URI. This is fundamental to federation:

```
https://example.com/users/alice/objects/abc123
```

Without unique URIs, other servers cannot reference, fetch, or interact with your objects.

### 2. **Collection Pagination**
ActivityPub uses a two-tier collection model:

- **OrderedCollection**: Metadata wrapper with total count and link to first page
- **OrderedCollectionPage**: Actual items with navigation (next/prev) links

This allows efficient browsing of large collections without loading all items at once.

### 3. **Collection Enumeration**
Collections are read-only views of filtered data:
- **Inbox/Outbox**: All activities sent/received
- **Followers/Following**: Social graph relationships
- **Likes/Shares**: Engagement on objects
- **Replies**: Threaded conversations

## Implementation Details

### New Controllers

#### 1. **ObjectController** (`/users/{username}/objects/*`)

Serves individual objects with unique URIs and their associated collections:

```csharp
// Get an object
GET /users/alice/objects/123

// Get replies to an object
GET /users/alice/objects/123/replies

// Get likes on an object
GET /users/alice/objects/123/likes

// Get shares of an object
GET /users/alice/objects/123/shares
```

Each collection endpoint supports pagination via `page` and `limit` query parameters.

#### 2. **Enhanced ActorController**

Added actor-level collection endpoints:

```csharp
// Objects liked by this actor
GET /users/alice/liked

// Objects shared by this actor  
GET /users/alice/shared
```

#### 3. **Enhanced Inbox/OutboxController**

Updated to support proper pagination:
- Without params or with defaults → Returns `OrderedCollection` wrapper
- With pagination params → Returns `OrderedCollectionPage` with items

### Updated Interfaces

#### IActivityRepository

Extended with 10 new methods for collection queries:

**Object-level collections:**
- `GetRepliesAsync` / `GetRepliesCountAsync`
- `GetLikesAsync` / `GetLikesCountAsync`
- `GetSharesAsync` / `GetSharesCountAsync`

**Actor-level collections:**
- `GetLikedByActorAsync` / `GetLikedByActorCountAsync`
- `GetSharedByActorAsync` / `GetSharedByActorCountAsync`

All methods support pagination with `limit` and `offset` parameters.

### Pagination Pattern

The implementation uses a consistent pattern across all collections:

```csharp
if (page == 0 && limit == 20)
{
    // Return collection wrapper
    return new OrderedCollection 
    {
        Id = collectionUri,
        TotalItems = totalCount,
        First = new Link { Href = firstPageUri }
    };
}
else
{
    // Return collection page
    return new OrderedCollectionPage
    {
        Id = pageUri,
        PartOf = collectionUri,
        OrderedItems = items,
        Next = nextPageUri,
        Prev = prevPageUri
    };
}
```

## Architectural Benefits

### 1. **Proper Federation**
- Objects have stable URIs that other servers can reference
- Collections are paginated for efficient data transfer
- Follows ActivityPub spec for maximum compatibility

### 2. **Scalability**
- Pagination prevents loading entire collections into memory
- Lazy evaluation of collection items
- Efficient database queries with limit/offset

### 3. **Flexibility**
- Repository interface allows different storage backends
- In-memory implementation for testing
- Easy to add database-backed implementation

### 4. **Extensibility**
- Pattern is consistent across all collection types
- Easy to add new collection endpoints
- Clear separation between controller and data access

## Reading Collections

From a client perspective, reading a collection follows this pattern:

```csharp
// 1. Fetch the collection wrapper
var collection = await client.GetAsync<OrderedCollection>(
    "https://example.com/users/alice/outbox");

// 2. Follow the 'first' link to get the first page
var firstPageUri = collection.First.Href;
var page = await client.GetAsync<OrderedCollectionPage>(firstPageUri);

// 3. Process items
foreach (var item in page.OrderedItems)
{
    // Process each activity/object
}

// 4. Follow 'next' link for more pages
while (page.Next != null)
{
    page = await client.GetAsync<OrderedCollectionPage>(page.Next.Href);
    // Process more items
}
```

The Broca.ActivityPub.Client library handles this automatically via the `GetCollectionAsync` method.

## Next Steps

To make this fully functional, you need to:

### 1. **Implement Repository Methods**

The `InMemoryActivityRepository` currently returns empty collections. You need to:

- Index activities by type (Like, Announce, Create, etc.)
- Track object relationships (inReplyTo, object references)
- Query and filter based on these indexes

Example for likes:

```csharp
public Task<IEnumerable<IObjectOrLink>> GetLikesAsync(
    string objectId, int limit, int offset, CancellationToken ct)
{
    var likes = _activities.Values
        .OfType<Like>()
        .Where(like => like.Object?.Any(o => 
            o is Link link && link.Href?.ToString() == objectId ||
            o is Object obj && obj.Id == objectId) ?? false)
        .OrderByDescending(like => like.Published ?? DateTime.MinValue)
        .Skip(offset)
        .Take(limit);
    
    return Task.FromResult<IEnumerable<IObjectOrLink>>(likes);
}
```

### 2. **Update Object Creation**

When creating objects, ensure they get unique URIs:

```csharp
var objectId = Guid.NewGuid().ToString();
var note = new Note
{
    Id = $"{baseUrl}/users/{username}/objects/{objectId}",
    Content = content,
    Published = DateTime.UtcNow,
    AttributedTo = new Link { Href = new Uri($"{baseUrl}/users/{username}") },
    // Add collection links
    Replies = $"{baseUrl}/users/{username}/objects/{objectId}/replies",
    Likes = $"{baseUrl}/users/{username}/objects/{objectId}/likes",
    Shares = $"{baseUrl}/users/{username}/objects/{objectId}/shares"
};
```

### 3. **Database Schema**

For production, you'll need:

- Activities table with type, actor, object, published columns
- Indexes on frequently queried fields
- JSON columns for full ActivityStreams objects
- Relationship tracking for replies, likes, shares

### 4. **Caching**

Consider caching for frequently accessed collections:
- Collection counts
- Recent pages
- Popular objects

### 5. **Security**

Add authorization checks:
- Private inbox/outbox access
- Visibility controls (public, followers-only, direct)
- Rate limiting on collection endpoints

## Testing

Build test cases for:
- Pagination edge cases (empty collections, single item, large collections)
- Collection consistency (counts match actual items)
- URI generation (unique, dereferenceable)
- Federation scenarios (remote servers fetching collections)

## Resources

- [ActivityPub Spec - Collections](https://www.w3.org/TR/activitypub/#collections)
- [ActivityStreams - OrderedCollection](https://www.w3.org/TR/activitystreams-vocabulary/#dfn-orderedcollection)
- Rayven implementation (reference in this workspace)
- COLLECTIONS.md (endpoint documentation)

## Summary

This implementation provides the foundation for a fully-featured ActivityPub server with proper collection support. The pattern is consistent, extensible, and follows the spec. The main remaining work is implementing the storage layer to actually track and query the activity data.
