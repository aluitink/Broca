# System Actor Configuration for Outbound Federation

## Overview

The Broca ActivityPub server now uses a **system actor** to sign outbound federation requests when fetching remote actor profiles. This ensures that requests to other ActivityPub servers are properly authenticated according to the ActivityPub specification.

## What Changed

### InboxController Updates

The `InboxController` now uses the following components for secure outbound federation:

1. **IActivityPubClient** - Configured with system actor credentials for signing requests
2. **IMemoryCache** - Caches actor public keys to reduce network requests
3. **Removed IHttpClientFactory** - No longer making raw HTTP requests; using the client instead

### Public Key Caching

Public keys are now cached in memory with a 1-hour TTL to improve performance:
- **Cache Key Format**: `publickey:{keyId}`
- **Cache Duration**: 1 hour (configurable via `PublicKeyCacheDuration` constant)
- **Benefits**: Reduces latency and network overhead for repeated signature verifications

### Actor Extension Data

The implementation now properly extracts `publicKeyPem` from the Actor's `ExtensionData` dictionary:

```csharp
if (actor.ExtensionData != null && actor.ExtensionData.TryGetValue("publicKey", out var publicKeyObj))
{
    // Extract publicKeyPem from JsonElement or Dictionary
}
```

## Configuration

### 1. System Actor Setup

The system actor represents your server in federation. Configure it in `appsettings.json`:

```json
{
  "ActivityPubServer": {
    "BaseUrl": "https://yourdomain.com",
    "PrimaryDomain": "yourdomain.com",
    "SystemActorUsername": "sys",
    "RequireHttpSignatures": true
  },
  "ActivityPubClient": {
    "ActorId": "https://yourdomain.com/users/sys",
    "PublicKeyId": "https://yourdomain.com/users/sys#main-key",
    "PrivateKeyPem": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----",
    "EnableCaching": true,
    "CacheDuration": "01:00:00"
  }
}
```

### 2. Generate System Actor Keys

You'll need to generate an RSA key pair for the system actor:

```bash
# Generate private key
openssl genrsa -out sys_private.pem 2048

# Extract public key
openssl rsa -in sys_private.pem -outform PEM -pubout -out sys_public.pem

# Display private key for appsettings (copy this)
cat sys_private.pem
```

### 3. Create System Actor Profile

The system actor must exist in your database with the public key. Example initialization:

```csharp
public async Task EnsureSystemActorExists(
    IActorRepository actorRepository,
    ActivityPubServerOptions serverOptions,
    ActivityPubClientOptions clientOptions)
{
    var systemActor = await actorRepository.GetActorByUsernameAsync(serverOptions.SystemActorUsername);
    
    if (systemActor == null)
    {
        systemActor = new Actor
        {
            Id = serverOptions.SystemActorId,
            Type = new[] { "Application" },
            PreferredUsername = serverOptions.SystemActorUsername,
            Name = serverOptions.ServerName,
            Summary = $"System actor for {serverOptions.PrimaryDomain}",
            Inbox = $"{serverOptions.BaseUrl}/users/{serverOptions.SystemActorUsername}/inbox",
            Outbox = $"{serverOptions.BaseUrl}/users/{serverOptions.SystemActorUsername}/outbox",
            Followers = $"{serverOptions.BaseUrl}/users/{serverOptions.SystemActorUsername}/followers",
            Following = $"{serverOptions.BaseUrl}/users/{serverOptions.SystemActorUsername}/following",
            
            // Add public key to extension data
            ExtensionData = new Dictionary<string, object>
            {
                ["publicKey"] = new Dictionary<string, object>
                {
                    ["id"] = clientOptions.PublicKeyId,
                    ["owner"] = serverOptions.SystemActorId,
                    ["publicKeyPem"] = await GetPublicKeyPemFromPrivateKey(clientOptions.PrivateKeyPem)
                }
            }
        };
        
        await actorRepository.SaveActorAsync(serverOptions.SystemActorUsername, systemActor);
    }
}
```

### 4. Dependency Injection Setup

Ensure the ActivityPub client is registered with system actor credentials:

```csharp
// In Program.cs or Startup.cs
services.AddActivityPubClient(options =>
{
    var serverOptions = configuration.GetSection("ActivityPubServer").Get<ActivityPubServerOptions>();
    var clientOptions = configuration.GetSection("ActivityPubClient").Get<ActivityPubClientOptions>();
    
    options.ActorId = serverOptions.SystemActorId;
    options.PublicKeyId = clientOptions.PublicKeyId;
    options.PrivateKeyPem = clientOptions.PrivateKeyPem;
    options.EnableCaching = true;
});

// Memory cache is required for public key caching
services.AddMemoryCache();
```

## How It Works

### Outbound Request Flow

When the server needs to verify an incoming signature:

```
1. POST /users/bob/inbox (from remote server)
   ↓
2. Extract Signature header, get keyId
   ↓
3. Check memory cache for public key
   ↓
4. If not cached:
   a. Use IActivityPubClient.GetActorAsync(actorUrl)
   b. Client signs request with system actor private key
   c. Remote server receives signed GET request
   d. Remote server returns actor profile
   e. Extract publicKeyPem from actor.ExtensionData["publicKey"]
   f. Cache the key for 1 hour
   ↓
5. Verify signature using cached/fetched public key
   ↓
6. Process activity if signature valid
```

### Signed GET Request Example

When fetching a remote actor, the system actor signs the request:

```http
GET /users/alice HTTP/1.1
Host: mastodon.social
Date: Sun, 08 Dec 2025 15:30:00 GMT
Accept: application/activity+json
Signature: keyId="https://yourdomain.com/users/sys#main-key",
  algorithm="rsa-sha256",
  headers="(request-target) host date",
  signature="Base64EncodedSignature..."
```

## Benefits

### 1. **Proper Authentication**
- Remote servers can verify your server's identity
- Prevents rejection of requests from unknown sources
- Required by many Mastodon instances and other strict servers

### 2. **Performance**
- Public key caching reduces network roundtrips
- 1-hour cache duration balances freshness with performance
- Memory cache is fast and doesn't require external dependencies

### 3. **Type Safety**
- Uses strongly-typed Actor objects with ExtensionData
- Handles both JsonElement and Dictionary representations
- Proper null checking and error handling

### 4. **Compliance**
- Follows ActivityPub spec for federated authentication
- Compatible with Mastodon, Pleroma, and other implementations
- Uses standard HTTP Signatures (RSA-SHA256)

## Troubleshooting

### Problem: "The name '_activityPubClient' does not exist"
**Solution**: Ensure `IActivityPubClient` is registered in DI:
```csharp
services.AddActivityPubClient(options => { ... });
```

### Problem: "The name '_cache' does not exist"
**Solution**: Register memory cache in DI:
```csharp
services.AddMemoryCache();
```

### Problem: "Actor does not have publicKey in extension data"
**Solution**: Ensure the Actor's ExtensionData includes the publicKey:
```csharp
actor.ExtensionData = new Dictionary<string, object>
{
    ["publicKey"] = new {
        id = "...",
        owner = "...",
        publicKeyPem = "..."
    }
};
```

### Problem: Remote servers reject GET requests
**Solution**: Verify system actor credentials are configured:
1. Check `ActivityPubClient:PrivateKeyPem` is set
2. Check `ActivityPubClient:PublicKeyId` matches system actor
3. Verify system actor exists at `/users/sys`

## Security Considerations

### Private Key Storage
- **Never commit private keys to source control**
- Use environment variables or Azure Key Vault in production
- Rotate keys periodically (e.g., annually)

### Cache Security
- Memory cache is cleared on application restart
- No sensitive data persisted to disk
- Cache keys include full keyId to prevent collisions

### Key Rotation
If you need to rotate the system actor's keys:

1. Generate new key pair
2. Update `ActivityPubClient:PrivateKeyPem` in configuration
3. Update system actor's `publicKey.publicKeyPem` in database
4. Restart application to clear cache
5. Wait 1 hour for remote servers to refetch the new key

## Example: Complete System Actor

Here's what the system actor looks like when fetched:

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "id": "https://yourdomain.com/users/sys",
  "type": "Application",
  "preferredUsername": "sys",
  "name": "YourDomain ActivityPub Server",
  "summary": "System actor for yourdomain.com",
  "inbox": "https://yourdomain.com/users/sys/inbox",
  "outbox": "https://yourdomain.com/users/sys/outbox",
  "followers": "https://yourdomain.com/users/sys/followers",
  "following": "https://yourdomain.com/users/sys/following",
  "publicKey": {
    "id": "https://yourdomain.com/users/sys#main-key",
    "owner": "https://yourdomain.com/users/sys",
    "publicKeyPem": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----"
  }
}
```

## References

- [ActivityPub Specification](https://www.w3.org/TR/activitypub/)
- [HTTP Signatures](https://datatracker.ietf.org/doc/html/draft-cavage-http-signatures)
- [Mastodon Federation Documentation](https://docs.joinmastodon.org/spec/activitypub/)
