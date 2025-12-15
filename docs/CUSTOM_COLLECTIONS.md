# Custom Collections Feature

## Overview

The Custom Collections feature allows ActivityPub actors to define and manage custom collections beyond the standard `inbox`, `outbox`, `followers`, and `following` collections. This enables use cases like featured posts, pinned content, media galleries, and more.

## Key Features

- **Two Collection Types**:
  - **Manual Collections**: Items are explicitly added/removed by the user
  - **Query Collections**: Items are dynamically selected based on filter criteria

- **Visibility Levels**:
  - **Public**: Visible to everyone, advertised in actor profile
  - **Private**: Only visible when authenticated with actor's private key
  - **Unlisted**: Not advertised but accessible if URL is known

- **ActivityPub Integration**: Collections are created and managed via ActivityPub protocol messages

- **Flexible Querying**: Query collections support filtering by type, tags, dates, attachments, and more

## Architecture

### Core Components

1. **Models** (`CustomCollectionDefinition`)
   - Defines collection metadata and configuration
   - Supports both manual item lists and query filters
   - Located in `Broca.ActivityPub.Core/Models/`

2. **Service Layer** (`CollectionService`)
   - Manages collection CRUD operations
   - Executes query filters for dynamic collections
   - Validates collection definitions
   - Located in `Broca.ActivityPub.Server/Services/`

3. **Persistence Layer**
   - FileSystem: Stores collections as JSON files in `data/actors/{username}/collections/`
   - InMemory: Stores in concurrent dictionaries for testing

4. **Controller** (`CollectionsController`)
   - REST API for accessing collections
   - Returns ActivityPub-compliant OrderedCollection responses
   - Located in `Broca.ActivityPub.Server/Controllers/`

5. **Admin Handler Integration**
   - Accepts Collection Create activities via system inbox
   - Parses Broca-specific extension data

## Creating Collections

### Method 1: Via ActivityPub Protocol (Recommended)

Send a `Create` activity to the actor's inbox (or system inbox) with a Collection object containing Broca extension data:

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Create",
  "actor": "https://example.com/users/alice",
  "object": {
    "type": "Collection",
    "name": "Featured Posts",
    "summary": "My hand-picked featured posts",
    "attributedTo": "https://example.com/users/alice",
    "collectionDefinition": {
      "id": "featured",
      "name": "Featured Posts",
      "description": "My hand-picked featured posts",
      "type": "Manual",
      "visibility": "Public",
      "sortOrder": "Chronological"
    }
  }
}
```

### Method 2: Direct API (Requires Authentication)

POST to `/users/{username}/collections` with the collection definition in the request body.

## Collection Types

### Manual Collections

Users explicitly add/remove items. Perfect for curated content like:
- Featured posts
- Pinned content
- Favorites
- Reading lists

**Example Definition**:
```json
{
  "id": "featured",
  "name": "Featured Posts",
  "description": "My best posts",
  "type": "Manual",
  "visibility": "Public",
  "items": [],
  "maxItems": 10,
  "sortOrder": "Manual"
}
```

### Query Collections

Items are automatically selected based on filters. Perfect for:
- Media galleries (filter by `hasAttachment: true`)
- Articles (filter by `objectTypes: ["Article"]`)
- Tagged content (filter by `tags: ["photography"]`)
- Recent posts (filter by `afterDate`)

**Example Definition**:
```json
{
  "id": "media",
  "name": "Media Gallery",
  "description": "All my posts with images and videos",
  "type": "Query",
  "visibility": "Public",
  "queryFilter": {
    "hasAttachment": true,
    "objectTypes": ["Image", "Video", "Note"],
    "afterDate": "2025-01-01T00:00:00Z"
  },
  "maxItems": 50,
  "sortOrder": "Chronological"
}
```

## Query Filter Options

Query collections support rich filtering:

- **activityTypes**: Filter by activity type (e.g., `["Create", "Announce"]`)
- **objectTypes**: Filter by object type (e.g., `["Note", "Article", "Image"]`)
- **tags**: Filter by hashtags or tags
- **afterDate/beforeDate**: Date range filtering
- **hasAttachment**: Only items with/without attachments
- **isReply**: Only replies or only top-level posts
- **visibility**: Filter by visibility level
- **searchQuery**: Text search in content, name, and summary
- **customFilters**: JSONPath expressions for advanced filtering

## REST API

### Get Collections Catalog

```
GET /users/{username}/collections
Accept: application/activity+json
```

Returns an OrderedCollection listing all public collections for the user.

### Get Collection Items

```
GET /users/{username}/collections/{collectionId}?page=0&limit=20
Accept: application/activity+json
```

Returns an OrderedCollectionPage with items in the collection.

### Get Collection Definition

```
GET /users/{username}/collections/{collectionId}/definition
Accept: application/json
```

Returns the CustomCollectionDefinition JSON.

### Add Item to Manual Collection

```
POST /users/{username}/collections/{collectionId}/items
Content-Type: application/json
Authorization: Bearer {token}

{
  "itemId": "https://example.com/users/alice/posts/123"
}
```

### Remove Item from Manual Collection

```
DELETE /users/{username}/collections/{collectionId}/items/{itemId}
Authorization: Bearer {token}
```

## Actor Profile Integration

Public collections are automatically advertised in the actor's profile via the `broca:collections` extension:

```json
{
  "@context": [
    "https://www.w3.org/ns/activitystreams",
    "https://w3id.org/security/v1"
  ],
  "type": "Person",
  "id": "https://example.com/users/alice",
  "preferredUsername": "alice",
  "inbox": "https://example.com/users/alice/inbox",
  "outbox": "https://example.com/users/alice/outbox",
  "broca:collections": {
    "collections": "https://example.com/users/alice/collections",
    "featured": "https://example.com/users/alice/collections/featured",
    "media": "https://example.com/users/alice/collections/media"
  }
}
```

## Usage Examples

### Example 1: Featured Posts Collection

Create a manual collection for featuring your best posts:

1. Create the collection via ActivityPub
2. Add posts by posting to the collection's items endpoint
3. The collection appears in your actor profile
4. Other servers can fetch and display your featured posts

### Example 2: Photography Gallery

Create a query collection that automatically includes all posts with image attachments and the #photography tag:

```json
{
  "id": "photography",
  "name": "Photography",
  "type": "Query",
  "visibility": "Public",
  "queryFilter": {
    "hasAttachment": true,
    "tags": ["photography"],
    "objectTypes": ["Image", "Note"]
  },
  "sortOrder": "Chronological",
  "maxItems": 100
}
```

### Example 3: Private Drafts Collection

Create a private collection for draft posts:

```json
{
  "id": "drafts",
  "name": "Drafts",
  "type": "Manual",
  "visibility": "Private"
}
```

## Implementation Notes

### Storage

- **FileSystem**: Collections are stored as `{collectionId}.json` files in `data/actors/{username}/collections/`
- **InMemory**: Collections are stored in `ConcurrentDictionary` for testing

### Performance

- Query collections evaluate filters on-demand when accessed
- Consider using `maxItems` to limit collection size
- Pagination is supported for large collections

### Security

- Private collections require authentication (TODO: implement authentication)
- Collection creation requires authorization
- Items can only be added to collections owned by the authenticated user

### Extensibility

The `customFilters` field in QueryFilter allows for future JSONPath-based filtering without breaking changes.

## Future Enhancements

- [ ] Authentication/authorization for private collections
- [ ] Collection sharing/collaboration
- [ ] Collection templates
- [ ] Advanced query operators (AND, OR, NOT)
- [ ] Collection subscriptions (ActivityPub Follow for collections)
- [ ] Collection statistics and analytics
- [ ] Import/export functionality

## Related Files

- Models: `src/Broca.ActivityPub.Core/Models/CustomCollectionDefinition.cs`
- Service: `src/Broca.ActivityPub.Server/Services/CollectionService.cs`
- Controller: `src/Broca.ActivityPub.Server/Controllers/CollectionsController.cs`
- Interface: `src/Broca.ActivityPub.Core/Interfaces/ICollectionService.cs`
- Repository: `src/Broca.ActivityPub.Core/Interfaces/IActorRepository.cs`
- Tests: `tests/Broca.ActivityPub.IntegrationTests/CustomCollectionsTests.cs`
