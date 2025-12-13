# ActivityPub Server Collection Endpoints

This document describes the ActivityPub collection endpoints implemented in Broca.ActivityPub.Server.

## Overview

The server implements the ActivityPub specification's collection model using `OrderedCollection` and `OrderedCollectionPage` for pagination. All objects are identified by unique, dereferenceable URIs as required by the ActivityPub spec.

## Collection Structure

### OrderedCollection (Collection Wrapper)
When requesting a collection without pagination parameters (or with default `page=0&limit=20`), you get the collection wrapper:

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "id": "https://example.com/users/alice/outbox",
  "type": "OrderedCollection",
  "totalItems": 42,
  "first": "https://example.com/users/alice/outbox?page=0&limit=20"
}
```

### OrderedCollectionPage (Paginated Results)
When requesting a specific page, you get an `OrderedCollectionPage`:

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "id": "https://example.com/users/alice/outbox?page=1&limit=20",
  "type": "OrderedCollectionPage",
  "partOf": "https://example.com/users/alice/outbox",
  "totalItems": 42,
  "orderedItems": [...],
  "next": "https://example.com/users/alice/outbox?page=2&limit=20",
  "prev": "https://example.com/users/alice/outbox?page=0&limit=20"
}
```

## Actor Endpoints

### GET /users/{username}
Returns the Actor object for the specified user.

**Response**: `application/activity+json`

### GET /users/{username}/followers
Returns the collection of actors following this user.

**Query Parameters**:
- `page` (optional): Page number (default: 0)
- `limit` (optional): Items per page (default: 20)

**Response**: `OrderedCollection` or `OrderedCollectionPage`

### GET /users/{username}/following
Returns the collection of actors this user follows.

**Query Parameters**:
- `page` (optional): Page number (default: 0)
- `limit` (optional): Items per page (default: 20)

**Response**: `OrderedCollection` or `OrderedCollectionPage`

### GET /users/{username}/liked
Returns the collection of objects liked by this user.

**Query Parameters**:
- `page` (optional): Page number (default: 0)
- `limit` (optional): Items per page (default: 20)

**Response**: `OrderedCollection` or `OrderedCollectionPage`

### GET /users/{username}/shared
Returns the collection of objects shared (announced) by this user.

**Query Parameters**:
- `page` (optional): Page number (default: 0)
- `limit` (optional): Items per page (default: 20)

**Response**: `OrderedCollection` or `OrderedCollectionPage`

## Inbox/Outbox Endpoints

### GET /users/{username}/inbox
Returns the user's inbox collection (received activities).

**Query Parameters**:
- `page` (optional): Page number (default: 0)
- `limit` (optional): Items per page (default: 20)

**Response**: `OrderedCollection` or `OrderedCollectionPage`

### POST /users/{username}/inbox
Receives an activity from another ActivityPub server.

**Request**: `application/activity+json`
**Response**: `202 Accepted` on success

### GET /users/{username}/outbox
Returns the user's outbox collection (sent activities).

**Query Parameters**:
- `page` (optional): Page number (default: 0)
- `limit` (optional): Items per page (default: 20)

**Response**: `OrderedCollection` or `OrderedCollectionPage`

### POST /users/{username}/outbox
Publishes a new activity from the user.

**Request**: `application/activity+json`
**Response**: `201 Created` with activity ID

## Object Endpoints

All objects (Notes, Articles, etc.) are identified by unique URIs following the pattern:
`https://example.com/users/{username}/objects/{objectId}`

### GET /users/{username}/objects/{objectId}
Returns a specific object/activity by ID.

**Response**: The object with the appropriate ActivityStreams type

### GET /users/{username}/objects/{objectId}/replies
Returns replies to the specified object.

**Query Parameters**:
- `page` (optional): Page number (default: 0)
- `limit` (optional): Items per page (default: 20)

**Response**: `OrderedCollection` or `OrderedCollectionPage` containing reply objects

### GET /users/{username}/objects/{objectId}/likes
Returns likes for the specified object.

**Query Parameters**:
- `page` (optional): Page number (default: 0)
- `limit` (optional): Items per page (default: 20)

**Response**: `OrderedCollection` or `OrderedCollectionPage` containing Like activities

### GET /users/{username}/objects/{objectId}/shares
Returns shares/announces for the specified object.

**Query Parameters**:
- `page` (optional): Page number (default: 0)
- `limit` (optional): Items per page (default: 20)

**Response**: `OrderedCollection` or `OrderedCollectionPage` containing Announce activities

## Implementation Notes

### Unique Object URIs
Every object created by the server MUST have a unique, dereferenceable URI. When creating objects:

```csharp
var note = new Note
{
    Id = $"{baseUrl}/users/{username}/objects/{objectId}",
    // ... other properties
};
```

### Collection Links in Objects
Objects should include links to their related collections:

```json
{
  "id": "https://example.com/users/alice/objects/123",
  "type": "Note",
  "content": "Hello, world!",
  "replies": "https://example.com/users/alice/objects/123/replies",
  "likes": "https://example.com/users/alice/objects/123/likes",
  "shares": "https://example.com/users/alice/objects/123/shares"
}
```

### Repository Requirements

The `IActivityRepository` interface must be implemented to support the following methods:

**For object collections**:
- `GetRepliesAsync(objectId, limit, offset)` / `GetRepliesCountAsync(objectId)`
- `GetLikesAsync(objectId, limit, offset)` / `GetLikesCountAsync(objectId)`
- `GetSharesAsync(objectId, limit, offset)` / `GetSharesCountAsync(objectId)`

**For actor collections**:
- `GetLikedByActorAsync(username, limit, offset)` / `GetLikedByActorCountAsync(username)`
- `GetSharedByActorAsync(username, limit, offset)` / `GetSharedByActorCountAsync(username)`

### Pagination Logic

Collections use a two-tier approach:

1. **Collection Wrapper** (`page=0, limit=20`): Returns metadata with a `first` link to the actual items
2. **Collection Page** (any other params): Returns the actual items with `next`/`prev` links

This follows the ActivityPub spec's recommendation for efficient collection browsing.

## Security Considerations

- Inbox POST requests should verify HTTP signatures (currently not implemented)
- Consider implementing authentication for reading private collections
- Rate limiting should be applied to prevent abuse
- Validate all incoming ActivityPub objects before storage

## Federation

These endpoints enable full ActivityPub federation:

1. Other servers can discover actors via WebFinger
2. Follow activities create follower/following relationships
3. Inbox receives federated activities (Create, Like, Announce, etc.)
4. Outbox publishes activities to followers' inboxes
5. Objects are uniquely addressable and can be referenced across servers
6. Collections are paginated for efficient traversal

## Example Workflow

1. **Create a Note**:
   ```
   POST /users/alice/outbox
   { "type": "Create", "object": { "type": "Note", "content": "Hello" } }
   ```

2. **Object gets a unique ID**:
   ```
   https://example.com/users/alice/objects/abc123
   ```

3. **Like the Note**:
   ```
   POST /users/bob/outbox
   { "type": "Like", "object": "https://example.com/users/alice/objects/abc123" }
   ```

4. **View likes**:
   ```
   GET /users/alice/objects/abc123/likes
   ```

5. **Retrieve the object**:
   ```
   GET /users/alice/objects/abc123
   ```
