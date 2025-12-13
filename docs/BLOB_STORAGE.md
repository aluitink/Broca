# Blob Storage Implementation

This document describes the blob storage facility for handling media attachments in the Broca ActivityPub server.

## Overview

The blob storage system provides a simple file system-based implementation for storing and serving media attachments (images, videos, audio files, documents) associated with ActivityPub objects. When activities contain attachments, the system:

1. Downloads remote attachments to local storage
2. Rewrites URLs to use site-local URLs
3. Serves attachments from the local server

This ensures that activities served from inbox/outbox collections use stable, local URLs for media content.

## Architecture

### Components

#### 1. `IBlobStorageService` Interface

Located in `Broca.ActivityPub.Core/Interfaces/IBlobStorageService.cs`

Provides abstraction for blob storage operations:
- `StoreBlobAsync()` - Store binary content and get public URL
- `GetBlobAsync()` - Retrieve blob content by ID
- `DeleteBlobAsync()` - Delete a blob
- `BlobExistsAsync()` - Check if blob exists
- `BuildBlobUrl()` - Generate public URL for a blob

#### 2. `FileSystemBlobStorageService` Implementation

Located in `Broca.ActivityPub.Persistence/FileSystem/FileSystemBlobStorageService.cs`

File system implementation that:
- Stores blobs in `{DataPath}/blobs/{username}/{blobId}`
- Stores metadata alongside blobs (`.meta` files)
- Sanitizes filenames to prevent directory traversal
- Detects MIME types from file extensions
- Supports common media formats (images, videos, audio, documents)

#### 3. `AttachmentProcessingService`

Located in `Broca.ActivityPub.Server/Services/AttachmentProcessingService.cs`

Handles attachment processing:
- Downloads remote attachments and stores them locally
- Rewrites attachment URLs to use local blob storage
- Processes both direct attachments and images
- Handles nested objects within activities (Create/Update/Announce)

#### 4. `MediaController`

Located in `Broca.ActivityPub.Server/Controllers/MediaController.cs`

REST API endpoints:
- `GET /users/{username}/media/{blobId}` - Retrieve media blob
- `POST /users/{username}/media` - Upload media (requires auth - to be implemented)
- `DELETE /users/{username}/media/{blobId}` - Delete media (requires auth - to be implemented)

## URL Structure

Blobs are served from URLs following this pattern:

```
{BaseUrl}{RoutePrefix}/users/{username}/media/{blobId}
```

**Example:**
```
https://example.com/ap/users/alice/media/avatar.png
```

## Storage Structure

Blobs are stored in the file system with this structure:

```
{DataPath}/
  blobs/
    alice/
      avatar.png
      avatar.png.meta
      1702998400_a1b2c3d4.jpg
      1702998400_a1b2c3d4.jpg.meta
    bob/
      profile.jpg
      profile.jpg.meta
```

Metadata files (`.meta`) contain JSON with:
- `blobId` - Original blob identifier
- `contentType` - MIME type
- `storedAt` - Timestamp
- `filePath` - Full file path

## Integration

### Service Registration

The blob storage service is automatically registered when using file system persistence:

```csharp
// In Program.cs or Startup.cs
services.AddFileSystemPersistence(dataPath);
```

This registers:
- `IBlobStorageService` → `FileSystemBlobStorageService`
- `AttachmentProcessingService`

### Automatic Processing

Attachments are automatically processed in two scenarios:

#### 1. Inbox Processing

When activities arrive at the inbox (via `InboxProcessor`):
- Remote attachments are downloaded
- Stored in local blob storage
- URLs rewritten before saving to database

#### 2. Inbox/Outbox Retrieval

When serving activities from inbox/outbox (via controllers):
- Attachment URLs are rewritten to local URLs if blobs exist locally
- Ensures consistent local URLs in responses

## Configuration

### File System Options

Configure via `appsettings.json`:

```json
{
  "Persistence": {
    "DataPath": "./data"
  },
  "ActivityPub": {
    "BaseUrl": "https://example.com",
    "RoutePrefix": "ap"
  }
}
```

- `DataPath` - Root directory for all persistence (including blobs)
- `BaseUrl` - Public base URL for the server
- `RoutePrefix` - URL prefix for ActivityPub endpoints

## Supported Media Types

The implementation includes built-in support for:

### Images
- JPEG (`.jpg`, `.jpeg`)
- PNG (`.png`)
- GIF (`.gif`)
- WebP (`.webp`)
- SVG (`.svg`)
- BMP (`.bmp`)
- ICO (`.ico`)

### Videos
- MP4 (`.mp4`)
- WebM (`.webm`)
- Ogg (`.ogv`)
- AVI (`.avi`)
- QuickTime (`.mov`)
- Matroska (`.mkv`)

### Audio
- MP3 (`.mp3`)
- Ogg (`.ogg`)
- WAV (`.wav`)
- FLAC (`.flac`)
- M4A (`.m4a`)

### Documents
- PDF (`.pdf`)
- Text (`.txt`)
- HTML (`.html`)
- JSON (`.json`)
- XML (`.xml`)

## Usage Examples

### Processing Attachments in Activities

Attachments are automatically processed when activities are received. The `InboxProcessor` calls `AttachmentProcessingService` which:

1. Checks if attachment URLs are already local
2. Downloads remote attachments via HTTP
3. Stores them in local blob storage
4. Rewrites the URL to the local endpoint

### Serving Media

Media is served via the `MediaController`:

```http
GET /users/alice/media/avatar.png HTTP/1.1
Host: example.com
```

Response:
```http
HTTP/1.1 200 OK
Content-Type: image/png
Cache-Control: public, max-age=86400
ETag: "avatar.png"

[binary content]
```

### Manual Upload (Future Feature)

The upload endpoint is implemented but requires authentication:

```http
POST /users/alice/media HTTP/1.1
Host: example.com
Content-Type: image/png
Authorization: Bearer <token>

[binary content]
```

Response:
```json
{
  "url": "https://example.com/ap/users/alice/media/1702998400_a1b2c3d4.png",
  "blobId": "1702998400_a1b2c3d4.png",
  "contentType": "image/png"
}
```

## Security Considerations

### Path Traversal Prevention

The implementation includes safeguards against directory traversal attacks:
- Username sanitization removes `..`, `/`, `\`
- Blob IDs containing path separators are hashed
- All paths use `Path.GetFileName()` for final sanitization

### Future Authentication

Upload and delete endpoints are prepared for authentication but currently lack implementation. This should be added before deployment:

```csharp
// TODO: Add authentication/authorization to ensure only the owner can upload/delete
```

## Extending the Implementation

### Custom Blob Storage Provider

To implement a different storage backend (e.g., Azure Blob Storage, AWS S3):

1. Create a new class implementing `IBlobStorageService`
2. Register it in your DI container:

```csharp
services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
```

### Adding New MIME Types

Edit `FileSystemBlobStorageService.MimeTypes` dictionary:

```csharp
private static readonly Dictionary<string, string> MimeTypes = new()
{
    // Add new type
    { ".heic", "image/heic" },
    // ... existing types
};
```

## Performance Considerations

### Caching

The `MediaController` sets cache headers:
- `Cache-Control: public, max-age=86400` (24 hours)
- `ETag` for conditional requests

### Range Requests

The controller enables range request support for efficient video streaming:

```csharp
return File(stream, contentType, enableRangeProcessing: true);
```

### Parallel Processing

Attachment processing uses `Parallel.ForEachAsync` for efficient batch downloads when multiple attachments exist.

## Limitations

Current limitations to be aware of:

1. **No authentication** - Upload/delete endpoints lack authentication
2. **No quota management** - No limits on storage per user
3. **No cleanup** - No automatic deletion of orphaned blobs
4. **No deduplication** - Same file uploaded multiple times stores duplicates
5. **Single server** - File system storage doesn't scale across multiple servers

## Future Enhancements

Potential improvements:

- [ ] Add authentication for upload/delete operations
- [ ] Implement storage quotas per user
- [ ] Add blob garbage collection for orphaned files
- [ ] Implement content-addressed storage (deduplication)
- [ ] Add thumbnail generation for images
- [ ] Add transcoding for videos
- [ ] Implement Azure Blob Storage provider
- [ ] Implement AWS S3 provider
- [ ] Add virus scanning for uploads
- [ ] Implement access control lists (ACLs)

## Testing

To test the blob storage implementation:

1. **Receive an activity with attachments:**
   Send a Create activity to the inbox with Document attachments

2. **Verify storage:**
   Check that files appear in `{DataPath}/blobs/{username}/`

3. **Retrieve media:**
   Access the media URL and verify content is served correctly

4. **Verify URL rewriting:**
   Fetch inbox/outbox and confirm attachment URLs use local endpoints

## Troubleshooting

### Blobs not downloading

Check logs for:
- HTTP errors when downloading remote URLs
- Network connectivity issues
- SSL/TLS certificate validation failures

### Blobs not served

Verify:
- File permissions on blob storage directory
- Correct URL construction (BaseUrl + RoutePrefix)
- Controller route registration

### High disk usage

Implement:
- Storage quotas
- Garbage collection for orphaned blobs
- Deduplication based on content hash

## Related Documentation

- [Activity Delivery Architecture](DELIVERY_ARCHITECTURE.md)
- [Identity Provider Guide](IDENTITY_PROVIDER.md)
- [Server Implementation](SERVER_IMPLEMENTATION.md)
