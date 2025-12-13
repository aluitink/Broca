# Media Handling and Actor Endpoints

## Overview

This document explains how media attachments work in ActivityPub and what endpoints should be advertised in Actor objects.

## Media Upload Patterns

### Pattern 1: Mastodon Client API (NOT for federation)
```
POST /api/v2/media
```
- **Purpose**: Mastodon mobile/web apps upload media BEFORE creating a post
- **Authentication**: OAuth user token
- **Not part of ActivityPub spec**: This is Mastodon's proprietary API
- **You don't need this for federation**

### Pattern 2: ActivityPub Federation (What you need)
```
POST /users/{username}/media (authenticated)
GET  /users/{username}/media/{blobId} (public)
```
- **Purpose**: Server-to-server federation
- **How it works**:
  1. User uploads media to their own server (via your app/API)
  2. Server stores blob, generates URL like `https://example.com/users/alice/media/abc123.jpg`
  3. User creates post with attachment
  4. Server sends Create activity with attachment URL to followers
  5. Remote servers fetch media via GET request
  6. Remote servers cache the media locally

**Your current implementation is correct!**

## Actor Endpoints Property

The `endpoints` property in Actor objects advertises server capabilities.

### Standard Endpoints

```json
{
  "type": "Person",
  "id": "https://example.com/users/alice",
  "inbox": "https://example.com/users/alice/inbox",
  "outbox": "https://example.com/users/alice/outbox",
  "endpoints": {
    "sharedInbox": "https://example.com/inbox",
    "uploadMedia": "https://example.com/users/alice/media",
    "proxyUrl": "https://example.com/proxy"
  }
}
```

### Endpoint Descriptions

#### sharedInbox (Important)
- **Purpose**: Efficient delivery for servers with many followers
- **Example**: Instead of sending to 1000 individual inboxes on `mastodon.social`, send once to their shared inbox
- **Performance**: Massively reduces network traffic and processing
- **Recommended**: Implement this for production

#### uploadMedia (Optional - Mastodon Extension)
- **Purpose**: Advertises where authenticated clients can upload media
- **Note**: This is a Mastodon extension, not in the ActivityPub spec
- **Usage**: Mainly for Mastodon mobile apps, not for federation
- **Optional**: You can include it if you want Mastodon apps to work with your server

#### proxyUrl (Optional)
- **Purpose**: Proxy for fetching remote media through your server
- **Privacy benefit**: Hide user IP addresses when fetching remote media
- **Bandwidth consideration**: Increases your server's bandwidth usage
- **Optional**: Most servers don't implement this

## Implementation Example

### Adding Endpoints to Actor

Since you're using `KristofferStrube.ActivityStreams`, the Actor class supports `ExtensionData` for custom properties:

```csharp
[HttpGet]
[Produces("application/activity+json", "application/ld+json")]
public async Task<IActionResult> Get(string username)
{
    var actor = await _actorRepository.GetActorByUsernameAsync(username);
    if (actor == null)
    {
        return NotFound(new { error = "Actor not found" });
    }

    // Add endpoints property
    var baseUrl = $"{Request.Scheme}://{Request.Host}";
    actor.ExtensionData ??= new Dictionary<string, JsonElement>();
    
    actor.ExtensionData["endpoints"] = JsonSerializer.SerializeToElement(new
    {
        sharedInbox = $"{baseUrl}/inbox",  // Recommended
        uploadMedia = $"{baseUrl}/users/{username}/media"  // Optional
    }, new JsonSerializerOptions 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    });

    return Ok(actor);
}
```

### Shared Inbox Implementation

A shared inbox is just like a user inbox, but processes deliveries for all users:

```csharp
[HttpPost("inbox")]
[Consumes("application/activity+json", "application/ld+json")]
public async Task<IActionResult> PostToSharedInbox()
{
    // 1. Verify HTTP signature
    // 2. Parse activity
    // 3. Determine recipient(s) from activity's 'to', 'cc', 'bcc'
    // 4. Deliver to local recipients' inboxes
    
    return Accepted();
}
```

## Media Attachment Workflow

### Complete Federation Flow

1. **Alice uploads image to her server**
   ```
   POST https://alice.example/users/alice/media
   → Returns: { "url": "https://alice.example/users/alice/media/photo123.jpg" }
   ```

2. **Alice creates a post with the image**
   ```json
   {
     "type": "Create",
     "actor": "https://alice.example/users/alice",
     "object": {
       "type": "Note",
       "content": "Check out this photo!",
       "attachment": [
         {
           "type": "Image",
           "mediaType": "image/jpeg",
           "url": "https://alice.example/users/alice/media/photo123.jpg"
         }
       ]
     }
   }
   ```

3. **Alice's server delivers to followers**
   ```
   POST https://bob.example/users/bob/inbox
   POST https://mastodon.social/inbox (shared inbox)
   ```

4. **Bob's server processes the Create activity**
   ```
   GET https://alice.example/users/alice/media/photo123.jpg
   → Downloads and caches locally
   → Shows to Bob with local URL
   ```

## Security Considerations

### Media Upload
- **Authentication required**: Only the user should upload to their media endpoint
- **Rate limiting**: Prevent abuse
- **File validation**: Check MIME types, size limits, scan for malware
- **Storage quotas**: Limit per-user storage

### Media Serving
- **Public access**: Media URLs should be fetchable without auth (for federation)
- **CORS headers**: Allow cross-origin requests for web clients
- **Cache headers**: Use `Cache-Control` for efficient delivery
- **Content-Type**: Always serve correct MIME type

## Comparison with Mastodon

| Feature | Mastodon | Your Server | Notes |
|---------|----------|-------------|-------|
| `/api/v2/media` | ✅ Yes | ❌ Not needed | Client API only |
| `/users/{user}/media` | ✅ Yes | ✅ You have this | Federation GET |
| `sharedInbox` | ✅ Yes | ⚠️ Should add | Important for scale |
| `endpoints.uploadMedia` | ✅ Yes | ⚠️ Optional | Mastodon extension |
| HTTP Signatures | ✅ Yes | ✅ You have this | Required |

## Recommendations

### Must Have (Priority 1)
- ✅ Media serving via GET (you have this)
- ✅ Attachment URLs in activities (you have this)
- ⚠️ **Add `endpoints.sharedInbox` to Actor objects**
- ⚠️ **Implement shared inbox endpoint**

### Nice to Have (Priority 2)
- ⚠️ Add `endpoints.uploadMedia` to Actor (for Mastodon app compatibility)
- ⚠️ Media upload authentication (you have it, just needs implementation)
- ⚠️ Media deletion (you have it, needs auth)

### Optional (Priority 3)
- ⚠️ `endpoints.proxyUrl` for privacy
- ⚠️ Image thumbnail generation
- ⚠️ Video transcoding
- ⚠️ Media processing queues for large files

## References

- [ActivityPub Spec - Actor Endpoints](https://www.w3.org/TR/activitypub/#actor-objects)
- [Mastodon API - Media](https://docs.joinmastodon.org/methods/media/)
- [ActivityPub Primer](https://www.w3.org/TR/activitypub/#Overview)
- Your implementation: `MediaController.cs`
