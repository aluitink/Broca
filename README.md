# Broca

A modular .NET library for building ActivityPub-enabled applications.

## What is Broca?

Broca provides a complete, standards-compliant implementation of the ActivityPub protocol for .NET developers. It offers both client and server capabilities with a clean API that makes federation simple.

## Features

### Core ActivityPub Protocol

**Client-to-Server (C2S)**
- ✅ Post activities to outbox (Create, Like, Follow, Announce, Undo)
- ✅ Server-assigned activity IDs with proper URL structure
- ✅ Follow/Unfollow relationship management
- ✅ HTTP Signature authentication for outbox operations
- ✅ API key-based client authentication

**Server-to-Server (S2S) Federation**
- ✅ Cross-server activity delivery with HTTP Signatures
- ✅ Background delivery queue with retry logic
- ✅ Follow/Accept/Reject workflow (auto-accept and manual approval modes)
- ✅ Undo operations (Follow, Like, Announce)
- ✅ Delete activities with Tombstone support
- ✅ Update Person for profile changes
- ✅ Move activity for account migration with `alsoKnownAs` validation
- ✅ Date validation (reject stale or future-dated requests)
- ✅ Actor caching and refresh

**Shared Inbox**
- ✅ Efficient batch delivery to multiple local users
- ✅ To, Cc, and Bcc addressing support
- ✅ Public addressing (`https://www.w3.org/ns/activitystreams#Public`)
- ✅ Followers collection addressing
- ✅ Smart routing (mixed local/remote recipient handling)

### Collections & Discovery

- ✅ Followers and Following collections
- ✅ Custom collections (manual curation and query-based)
- ✅ Ordered collection pagination
- ✅ WebFinger support for @user@domain discovery
- ✅ NodeInfo 2.0 and 2.1 (server metadata and statistics)

### Media & Attachments

- ✅ Blob storage for media files
- ✅ Media endpoint (`/users/:username/media/:blobId`)
- ✅ Attachments in Create activities
- ✅ Content-Type preservation and validation

### Security & Authentication

- ✅ HTTP Signatures (draft-cavage-http-signatures-12)
- ✅ Public key infrastructure with cryptographic key generation
- ✅ Request date validation (prevents replay attacks)
- ✅ Outbox authentication (users can only post as themselves)
- ✅ Signature verification for incoming federation

### Content Type Handling

- ✅ `application/activity+json`
- ✅ `application/ld+json`
- ✅ Profile parameter support (`application/ld+json; profile="..."`)
- ✅ Mastodon compatibility

### Administration

- ✅ Back-channel user management via ActivityPub protocol
- ✅ System actor for server-level operations
- ✅ Create/Update/Delete users through admin endpoints
- ✅ Automatic key pair generation for new actors

### Persistence & Storage

- ✅ Modular storage abstractions (`IActorRepository`, `IActivityRepository`, `IBlobStorageService`)
- ✅ In-memory implementation (development/testing)
- ✅ File-based implementation (production-ready)
- ✅ Easy integration with custom storage backends

### Developer Experience

- ✅ Comprehensive integration test suite (88+ tests)
- ✅ Multi-server federation testing infrastructure
- ✅ Activity builder API for constructing valid ActivityStreams objects
- ✅ Blazor component library for building federated UIs
- ✅ Docker and Let's Encrypt support for production deployment

## Standards Compliance

Broca implements the core [ActivityPub W3C Recommendation](https://www.w3.org/TR/activitypub/) with extensions for real-world interoperability:

- ✅ **ActivityPub** - Client-to-Server and Server-to-Server protocols
- ✅ **ActivityStreams 2.0** - Core vocabulary and extended types
- ✅ **HTTP Signatures** - Request authentication (draft-cavage-http-signatures-12)
- ✅ **WebFinger** (RFC 7033) - User discovery via @username@domain
- ✅ **NodeInfo 2.0/2.1** - Server metadata and statistics

**Interoperability Target:** Mastodon (primary), with support for Threads, Pixelfed, and Pleroma.

**Known Limitations:**
- `featured` collection not yet exposed on actor documents (pinned posts)
- Follow/Following collections for locked accounts publicly visible (privacy enhancement pending)

All critical and high-priority federation features are complete. See [docs/s2s-compliance-todo.md](docs/s2s-compliance-todo.md) for detailed compliance tracking.

## Library Components

- **Broca.ActivityPub.Core** - Shared interfaces and models
- **Broca.ActivityPub.Client** - Client library for federated requests
- **Broca.ActivityPub.Server** - Server components for hosting actors
- **Broca.ActivityPub.Persistence** - Storage abstractions (in-memory, file-based)
- **Broca.ActivityPub.Components** - Blazor components for web UIs

## Quick Start: Using the Libraries

### Install Packages

```bash
# For client-side federation
dotnet add package Broca.ActivityPub.Client

# For hosting ActivityPub actors
dotnet add package Broca.ActivityPub.Server
```

### Client Example

```csharp
// Add to DI container
services.AddActivityPubClientAuthenticated(
    actorId: "https://myserver.com/users/alice",
    privateKeyPem: privateKey,
    publicKeyId: "https://myserver.com/users/alice#main-key"
);

// Use the client
var client = serviceProvider.GetRequiredService<IActivityPubClient>();

// Create and post a note
var note = client.CreateActivityBuilder()
    .CreateNote("Hello, fediverse!")
    .ToPublic()
    .Build();
    
await client.PostToOutboxAsync(note);
```

### Server Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add Broca server with all required services
builder.Services.AddBrocaServer(builder.Configuration);

// Add simple identity provider for basic actor setup
builder.Services.AddSimpleIdentityProvider(builder.Configuration);

var app = builder.Build();

// Initialize identities on startup
var identityService = app.Services.GetService<IdentityProviderService>();
if (identityService != null)
{
    await identityService.InitializeIdentitiesAsync();
}

app.MapControllers();
app.Run();
```

**appsettings.json:**
```json
{
  "ActivityPub": {
    "BaseUrl": "https://myblog.com",
    "PrimaryDomain": "myblog.com"
  },
  "IdentityProvider": {
    "SimpleIdentity": {
      "Username": "alice",
      "DisplayName": "Alice Smith",
      "Summary": "Personal blog about tech and life"
    }
  }
}
```

Now your application is followable as `@alice@myblog.com` from Mastodon and other federated platforms!

## Running the Sample Applications

The `samples/` folder contains two complete applications demonstrating Broca's capabilities:

### Broca.Sample.WebApi

A full-featured ActivityPub server with:
- WebFinger support for user discovery
- ActivityPub inbox/outbox endpoints
- File-based persistence
- System actor for server-level federation

### Broca.Sample.BlazorApp

A client-side Blazor WebAssembly application for:
- Looking up ActivityPub users by @handle
- Browsing user outboxes
- Testing client features in the browser

### Running with Docker Compose

The easiest way to run the samples locally:

```bash
cd samples
docker-compose up -d
```

This starts both applications:
- **WebApi**: http://localhost:5050
- **Blazor App**: http://localhost:5051

Test WebFinger:
```bash
curl "http://localhost:5050/.well-known/webfinger?resource=acct:user@localhost"
```

Test actor endpoint:
```bash
curl http://localhost:5050/ap/users/user
```

### Running without Docker

```bash
# Run the WebApi
cd samples/Broca.Sample.WebApi
dotnet run

# In another terminal, run the Blazor app
cd samples/Broca.Sample.BlazorApp
dotnet run
```

## Production Deployment with Let's Encrypt

ActivityPub **requires HTTPS** in production for federation. Here's how to deploy with automatic Let's Encrypt certificates.

### Prerequisites

- A domain name (e.g., `example.com`)
- A server with Docker installed
- DNS A record pointing your domain to your server's IP

### Step 1: Set Up nginx-proxy with Let's Encrypt

This creates a reverse proxy that automatically handles HTTPS certificates:

```bash
# Create the proxy network
docker network create nginx-proxy

# Start nginx-proxy
docker run -d -p 80:80 -p 443:443 \
  --name nginx-proxy \
  --network nginx-proxy \
  --restart always \
  -v /var/run/docker.sock:/tmp/docker.sock:ro \
  -v nginx-certs:/etc/nginx/certs \
  -v nginx-vhost:/etc/nginx/vhost.d \
  -v nginx-html:/usr/share/nginx/html \
  nginxproxy/nginx-proxy

# Start Let's Encrypt companion
docker run -d \
  --name nginx-proxy-acme \
  --network nginx-proxy \
  --restart always \
  --volumes-from nginx-proxy \
  -v /var/run/docker.sock:/var/run/docker.sock:ro \
  -v acme-state:/etc/acme.sh \
  -e DEFAULT_EMAIL=your-email@example.com \
  nginxproxy/acme-companion
```

### Step 2: Configure Broca for Production

In the `samples/` directory, create or edit `docker-compose.override.yml`:

```yaml
services:
  broca-api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ActivityPub__BaseUrl=https://example.com
      - ActivityPub__PrimaryDomain=example.com
      - VIRTUAL_HOST=example.com
      - VIRTUAL_PORT=8080
      - LETSENCRYPT_HOST=example.com
      - LETSENCRYPT_EMAIL=your-email@example.com
    networks:
      - broca-network
      - nginx-proxy

  broca-blazor:
    environment:
      - VIRTUAL_HOST=app.example.com
      - VIRTUAL_PORT=80
      - LETSENCRYPT_HOST=app.example.com
      - LETSENCRYPT_EMAIL=your-email@example.com
    networks:
      - broca-network
      - nginx-proxy

networks:
  nginx-proxy:
    external: true
```

### Step 3: Deploy

```bash
cd samples
docker-compose up -d
```

The acme-companion will automatically:
1. Request Let's Encrypt certificates for your domains
2. Configure nginx to serve your applications over HTTPS
3. Renew certificates before they expire

Your server will be accessible at:
- **API**: https://example.com
- **Blazor App**: https://app.example.com

Users can now follow actors like `@user@example.com` from Mastodon, Misskey, and other ActivityPub platforms!

### Verification

Test your production deployment:

```bash
# Test WebFinger
curl "https://example.com/.well-known/webfinger?resource=acct:user@example.com"

# Test actor endpoint
curl https://example.com/ap/users/user

# Verify HTTPS certificate
curl -vI https://example.com 2>&1 | grep "SSL certificate verify ok"
```

### Troubleshooting

**Certificate not generated?**
- Ensure DNS is properly configured and propagated
- Check logs: `docker logs nginx-proxy-acme`
- Verify port 80 is accessible (required for Let's Encrypt validation)

**Can't federate with Mastodon?**
- Ensure `ActivityPub__BaseUrl` uses HTTPS
- Verify your server is accessible from the internet
- Check HTTP Signature implementation if requests are rejected

## Custom Identity Providers

The SimpleIdentityProvider is great for single-user scenarios. For multi-user applications, implement `IIdentityProvider`:

```csharp
public class CustomIdentityProvider : IIdentityProvider
{
    public Task<ActorIdentity?> GetIdentityByUsernameAsync(string username)
    {
        // Load user from your database
        var user = await _userRepository.GetByUsernameAsync(username);
        
        return new ActorIdentity
        {
            Username = user.Username,
            DisplayName = user.DisplayName,
            Summary = user.Bio,
            PublicKeyPem = user.PublicKey,
            PrivateKeyPem = user.PrivateKey,
            ActorType = "Person"
        };
    }
    
    // Implement other required methods...
}

// Register in DI
builder.Services.AddSingleton<IIdentityProvider, CustomIdentityProvider>();
```

## Requirements

- .NET 9.0 or later
- Docker (for containerized deployment)
- A domain name with HTTPS for production federation

## Testing & Quality Assurance

Broca includes a comprehensive test suite to ensure reliability and standards compliance:

**Integration Tests** (88+ tests across 10 test suites)
- `ServerToServerTests` - Cross-server federation scenarios (22 tests)
- `SharedInboxTests` - Efficient multi-user delivery (11 tests)
- `ClientToServerTests` - Outbox posting and activity creation (7 tests)
- `CustomCollectionsTests` - Manual and query-based collections (15 tests)
- `AdminOperationsTests` - Back-channel user management (8 tests)
- `ClientAuthenticationTests` - API key and HTTP Signature auth (5 tests)
- `ContentTypeHandlingTests` - Mastodon compatibility (7 tests)
- `BlobStorageTests` - Media upload and retrieval (4 tests)
- `NodeInfoStatisticsTests` - Server metadata endpoints (5 tests)
- `OutboxAuthenticationTests` - Security validation (3 tests)

**Unit Tests**
- Repository implementations (actors, activities, delivery queue)
- Blob storage service functionality
- Component rendering logic

All tests use real HTTP clients and in-memory servers to validate end-to-end behavior, not mocked implementations. This ensures that Broca works correctly with actual ActivityPub clients and servers in the fediverse.

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](./CONTRIBUTING.md) for development workflow and guidelines.

## License

[Your license here]
