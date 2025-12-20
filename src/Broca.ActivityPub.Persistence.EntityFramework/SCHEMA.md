# Entity Framework Persistence Schema

This document describes the expanded, normalized relational database schema for the Broca ActivityPub EF persistence layer.

## Design Philosophy

The schema follows a **hybrid approach**:
- **Normalized relational data** for efficient querying and joins
- **Extracted ActivityPub/ActivityStreams fields** mapped to strongly-typed columns
- **Raw JSON preservation** for complete audit trail and backward compatibility
- **Denormalized counts** for performance with proper increment/decrement logic
- **Separate related tables** for recipients, attachments, and tags
- **Integer primary keys** with ActivityPub ID indexes for fast lookups

## Core Entities

### ActorEntity
Stores actor (user) profile information with full ActivityPub fields.

**Key Fields:**
- `Id` (bigint, PK) - Auto-increment primary key
- `Username` (string, unique) - Local username
- `ActorId` (string, unique) - ActivityPub ID (URI)
- `ActorType` (string) - Person, Service, Group, Organization, Application
- `PreferredUsername`, `DisplayName`, `Summary` - Profile information
- `IconUrl`, `ImageUrl` - Avatar and header images
- `PublicKeyPem`, `PrivateKeyPem` - Cryptographic keys
- `InboxUrl`, `OutboxUrl`, `FollowersUrl`, `FollowingUrl` - AP endpoints
- `ManuallyApprovesFollowers`, `Discoverable`, `Bot` - Privacy settings
- `FollowersCount`, `FollowingCount`, `StatusesCount` - Denormalized counts
- `ActorJson` (text) - Complete raw JSON
- `BlobStorageKey` - Reference to blob storage (optional)

**Indexes:**
- `Username` (unique)
- `ActorId` (unique)
- `PreferredUsername`
- `(Discoverable, Suspended)` composite

### ActivityEntity
Stores activities (Create, Update, Delete, Follow, Like, Announce, etc.) with extracted fields.

**Key Fields:**
- `Id` (bigint, PK) - Auto-increment primary key
- `ActivityId` (string, unique) - ActivityPub ID (URI)
- `Username` (string) - Local username owning this activity
- `ActivityType` (string) - Create, Update, Delete, Follow, Like, Announce, etc.
- `ActorId` (string) - Who performed the activity
- `ObjectId`, `ObjectType` - What was acted upon
- `TargetId`, `TargetType` - Target for Add/Remove operations
- `InReplyTo`, `ConversationId` - Threading information
- `Published`, `Updated` - Timestamps from ActivityPub
- `ContentText`, `ContentHtml`, `Summary` - Content fields (for Create activities)
- `Language`, `Sensitive` - Content metadata
- `IsPublic`, `IsInbox`, `IsOutbox` - Visibility and routing flags
- `RemoteUrl`, `BlobStorageKey` - External references
- `ReplyCount`, `LikeCount`, `ShareCount`, `AttachmentCount` - Denormalized counts
- `ActivityJson` (text) - Complete raw JSON

**Navigation Properties:**
- `Recipients` - Collection of ActivityRecipientEntity
- `Attachments` - Collection of ActivityAttachmentEntity
- `Tags` - Collection of ActivityTagEntity

**Indexes:**
- `ActivityId` (unique)
- `(Username, IsInbox, CreatedAt)` composite
- `(Username, IsOutbox, CreatedAt)` composite
- `(ActivityType, CreatedAt)` composite
- `ActorId`
- `ObjectId`
- `InReplyTo`
- `ConversationId`
- `(IsPublic, Published)` composite

### ObjectEntity
Stores ActivityPub objects (Notes, Articles, Images, Videos, etc.) separately from activities.

**Key Fields:**
- `Id` (bigint, PK) - Auto-increment primary key
- `ObjectId` (string, unique) - ActivityPub ID (URI)
- `ObjectType` (string) - Note, Article, Image, Video, etc.
- `AttributedToId` (string) - Actor who created this
- `AttributedToUsername` (string) - Local username if local actor
- `ContentText`, `ContentHtml`, `Summary`, `Name` - Content fields
- `Language`, `Sensitive` - Content metadata
- `InReplyTo`, `ConversationId` - Threading
- `Published`, `Updated`, `DeletedAt` - Timestamps
- `IsPublic` - Visibility flag
- `Url`, `RemoteUrl`, `BlobStorageKey` - External references
- `MediaType`, `Width`, `Height`, `Duration` - Media metadata
- `ReplyCount`, `LikeCount`, `ShareCount`, `AttachmentCount` - Denormalized counts
- `ObjectJson` (text) - Complete raw JSON

**Navigation Properties:**
- `Recipients` - Collection of ActivityRecipientEntity
- `Attachments` - Collection of ActivityAttachmentEntity
- `Tags` - Collection of ActivityTagEntity

**Indexes:**
- `ObjectId` (unique)
- `(ObjectType, Published)` composite
- `AttributedToId`
- `AttributedToUsername`
- `InReplyTo`
- `ConversationId`
- `(IsPublic, Published)` composite
- `DeletedAt`

## Relationship Entities

### ActivityRecipientEntity
Stores To/Cc/Bcc addressing for activities and objects.

**Key Fields:**
- `Id` (bigint, PK) - Auto-increment primary key
- `ActivityId` (bigint, FK, nullable) - Foreign key to ActivityEntity
- `ObjectId` (bigint, FK, nullable) - Foreign key to ObjectEntity
- `RecipientType` (string) - "To", "Cc", "Bcc"
- `RecipientAddress` (string) - Actor ID or collection URL
- `IsPublic`, `IsFollowers` - Quick lookup flags

**Indexes:**
- `(ActivityId, RecipientType, RecipientAddress)` composite
- `(ObjectId, RecipientType, RecipientAddress)` composite
- `(RecipientAddress, RecipientType)` composite
- `(IsPublic, CreatedAt)` composite

### ActivityAttachmentEntity
Stores media attachments for activities and objects.

**Key Fields:**
- `Id` (bigint, PK) - Auto-increment primary key
- `ActivityId` (bigint, FK, nullable) - Foreign key to ActivityEntity
- `ObjectId` (bigint, FK, nullable) - Foreign key to ObjectEntity
- `AttachmentType` (string) - Image, Video, Audio, Document
- `Url` (string) - URL to the attachment
- `MediaType` (string) - MIME type
- `Name` (string) - Alt text or filename
- `BlurhashValue` (string) - Blurhash for progressive loading
- `Width`, `Height`, `Duration`, `SizeBytes` - Media metadata
- `OrderIndex` (int) - Order in attachment list

**Indexes:**
- `(ActivityId, OrderIndex)` composite
- `(ObjectId, OrderIndex)` composite
- `AttachmentType`

### ActivityTagEntity
Stores tags (hashtags, mentions, emoji) for activities and objects.

**Key Fields:**
- `Id` (bigint, PK) - Auto-increment primary key
- `ActivityId` (bigint, FK, nullable) - Foreign key to ActivityEntity
- `ObjectId` (bigint, FK, nullable) - Foreign key to ObjectEntity
- `TagType` (string) - "Hashtag", "Mention", "Emoji"
- `Name` (string) - Tag name (e.g., "#activitypub", "@user@domain")
- `Href` (string) - URL for the tag (actor URL for mentions)
- `IconUrl`, `IconMediaType` - For custom emoji

**Indexes:**
- `(ActivityId, TagType, Name)` composite
- `(ObjectId, TagType, Name)` composite
- `(TagType, Name)` composite

## Existing Entities (Unchanged)

### FollowerEntity
Stores follower relationships.

**Key Fields:**
- `Id` (bigint, PK)
- `Username` (string) - Local user being followed
- `FollowerActorId` (string) - Actor ID of follower
- `CreatedAt` (datetime)

### FollowingEntity
Stores following relationships.

**Key Fields:**
- `Id` (bigint, PK)
- `Username` (string) - Local user doing the following
- `FollowingActorId` (string) - Actor ID being followed
- `CreatedAt` (datetime)

### DeliveryQueueEntity
Stores pending activity deliveries for federation.

**Key Fields:**
- `Id` (bigint, PK)
- `DeliveryId` (string, unique)
- `InboxUrl` (string)
- `SenderActorId`, `SenderUsername` (string)
- `ActivityJson` (text)
- `Status`, `AttemptCount`, `MaxRetries`
- Retry and completion timestamps

### CollectionDefinitionEntity
Stores custom collection definitions.

### CollectionItemEntity
Stores items in custom collections.

## Helper Services

### ActivityStreamExtractor
Service for extracting ActivityStreams data into normalized entities.

**Methods:**
- `ExtractActivityFields()` - Extracts core activity fields
- `ExtractRecipients()` - Extracts To/Cc/Bcc addressing
- `ExtractAttachments()` - Extracts media attachments
- `ExtractTags()` - Extracts hashtags, mentions, emoji

### CountManager
Service for managing denormalized counts with proper concurrency.

**Methods:**
- `IncrementReplyCountAsync()` / `DecrementReplyCountAsync()`
- `IncrementLikeCountAsync()` / `DecrementLikeCountAsync()`
- `IncrementShareCountAsync()` / `DecrementShareCountAsync()`
- `IncrementFollowerCountAsync()` / `DecrementFollowerCountAsync()`
- `IncrementFollowingCountAsync()` / `DecrementFollowingCountAsync()`
- `IncrementStatusCountAsync()` / `DecrementStatusCountAsync()`
- `RecalculateReplyCountAsync()` - Recalculates from actual data
- `RecalculateLikeCountAsync()` - Recalculates from actual data
- `RecalculateShareCountAsync()` - Recalculates from actual data

## Query Patterns

### Get public timeline
```sql
SELECT * FROM Activities 
WHERE IsPublic = 1 AND IsOutbox = 1 
ORDER BY Published DESC
```

### Get user's inbox
```sql
SELECT * FROM Activities 
WHERE Username = 'alice' AND IsInbox = 1 
ORDER BY CreatedAt DESC
```

### Get activity with recipients and attachments
```sql
SELECT a.*, r.*, at.*
FROM Activities a
LEFT JOIN ActivityRecipients r ON a.Id = r.ActivityId
LEFT JOIN ActivityAttachments at ON a.Id = at.ActivityId
WHERE a.ActivityId = 'https://example.com/activities/123'
```

### Get thread/conversation
```sql
SELECT * FROM Activities 
WHERE ConversationId = 'https://example.com/contexts/abc'
ORDER BY Published ASC
```

### Get hashtag usage
```sql
SELECT t.Name, COUNT(*) as Count
FROM ActivityTags t
WHERE t.TagType = 'Hashtag'
GROUP BY t.Name
ORDER BY Count DESC
```

## Data Integrity

### Cascade Deletes
- Deleting an Activity cascades to its Recipients, Attachments, and Tags
- Deleting an Object cascades to its Recipients, Attachments, and Tags

### Count Management
- Counts are updated via `CountManager` service using atomic SQL operations
- Periodic recalculation jobs can verify count accuracy
- SQL prevents negative counts with `WHERE count + delta >= 0` clauses

### Concurrency
- EF Core's optimistic concurrency on row version columns
- Atomic count updates via raw SQL
- Database-level unique constraints on ActivityId, ObjectId, ActorId

## Migration Strategy

When migrating from the minimal schema:
1. Add new columns with default values
2. Run data extraction jobs to populate new columns from JSON
3. Build indexes after data population
4. Update application code to use new fields
5. Keep JSON columns for backward compatibility

## Future Considerations

- **Blob Storage Integration**: Use `BlobStorageKey` to reference raw JSON in Azure Blob Storage, S3, etc.
- **Full-Text Search**: Add full-text indexes on ContentText, Summary for search
- **Partitioning**: Partition large tables by date or username for performance
- **Archival**: Move old activities to archive tables or blob storage
- **Read Replicas**: Use separate read replicas for timeline queries
