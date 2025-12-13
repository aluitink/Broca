# Broca ActivityPub Integration Tests

This is the new integration test framework for Broca ActivityPub, designed to test multi-server federation scenarios with realistic Client-to-Server (C2S) and Server-to-Server (S2S) interactions.

## Overview

The test framework is built around these key principles:

1. **Multiple Server Instances** - Tests can host multiple isolated server instances to simulate real federation
2. **In-Memory Repositories** - Each server has its own in-memory data stores, seeded with test data
3. **Client-Based Testing** - Tests use the ActivityPub client to make authenticated requests
4. **Background Delivery** - Tests poll for async S2S delivery completion
5. **Realistic Workflows** - Tests follow the actual C2S → S2S flow

## Architecture

### Infrastructure Components

- **`BrocaTestServer`** - Represents a single server instance with isolated repositories
- **`MultiServerTestFixture`** - Base class for tests requiring multiple servers
- **`TwoServerFixture`** - Convenient base for common two-server scenarios
- **`TestDataSeeder`** - Helpers for creating actors and activities
- **`TestClientFactory`** - Creates authenticated/unauthenticated ActivityPub clients
- **`ClientToServerHelper`** - Simplifies C2S interactions (posting to outbox, etc.)
- **`ServerToServerHelper`** - Provides polling utilities for S2S delivery verification
- **`KeyGenerator`** - Generates RSA key pairs for test actors

### Test Flow

```
1. Seed Users → In-memory repositories on each server
2. Create Client → Authenticated ActivityPub client with user's private key
3. C2S Post → Client posts to user's outbox via authenticated request
4. Outbox Processing → Server assigns unique ID and stores activity
5. S2S Delivery → Background job delivers to remote inboxes
6. Poll & Verify → Tests poll remote inbox until activity arrives
```

## Usage Examples

### Basic C2S Test

```csharp
public class MyTests : TwoServerFixture
{
    [Fact]
    public async Task UserCanPostToOutbox()
    {
        // Seed a user
        using var scope = ServerA.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        var (alice, privateKey) = await TestDataSeeder.SeedActorAsync(
            actorRepo, "alice", ServerA.BaseUrl);

        // Create authenticated client
        var client = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            alice.Id!,
            privateKey);

        var c2s = new ClientToServerHelper(client, alice.Id!, ClientA);

        // Post to outbox
        var activity = TestDataSeeder.CreateCreateActivity(
            alice.Id!, "Hello world!");
        var posted = await c2s.PostToOutboxAsync(activity);

        // Verify
        Assert.NotNull(posted.Id);
    }
}
```

### S2S Federation Test

```csharp
[Fact]
public async Task ActivityDeliveredAcrossServers()
{
    // Seed users on different servers
    var (alice, aliceKey) = await SeedUserAsync(ServerA, "alice");
    var (bob, bobKey) = await SeedUserAsync(ServerB, "bob");

    // Alice follows Bob
    var client = CreateAuthClient(ServerA, alice.Id!, aliceKey);
    var c2s = new ClientToServerHelper(client, alice.Id!, ClientA);
    
    var follow = TestDataSeeder.CreateFollow(alice.Id!, bob.Id!);
    await c2s.PostToOutboxAsync(follow);

    // Wait for delivery to Bob's inbox
    var s2s = new ServerToServerHelper(ServerB);
    var delivered = await s2s.WaitForInboxActivityByTypeAsync(
        "bob", "Follow", TimeSpan.FromSeconds(10));

    Assert.NotNull(delivered);
}
```

### Custom Multi-Server Setup

```csharp
public class ThreeServerTests : MultiServerTestFixture
{
    protected BrocaTestServer ServerA => GetServer("A");
    protected BrocaTestServer ServerB => GetServer("B");
    protected BrocaTestServer ServerC => GetServer("C");

    protected override async Task SetupServersAsync()
    {
        await CreateServerAsync("A", "https://server-a.test", "server-a.test");
        await CreateServerAsync("B", "https://server-b.test", "server-b.test");
        await CreateServerAsync("C", "https://server-c.test", "server-c.test");
    }
}
```

## Key Features

### Seeding Test Data

```csharp
var (actor, privateKey) = await TestDataSeeder.SeedActorAsync(
    actorRepository, "username", baseUrl);
```

This creates an actor with:
- Generated RSA key pair
- Proper inbox/outbox URLs
- ActivityStreams context

### Authenticated Requests

```csharp
var client = TestClientFactory.CreateAuthenticatedClient(
    () => server.CreateClient(),
    actorId,
    privateKeyPem);
```

Requests are automatically signed with HTTP Signatures.

### Polling for Delivery

Since deliveries happen in the background:

```csharp
var s2s = new ServerToServerHelper(server, timeout: TimeSpan.FromSeconds(10));

// Poll by type
var activity = await s2s.WaitForInboxActivityByTypeAsync("bob", "Follow");

// Poll by ID
var activity = await s2s.WaitForInboxActivityByIdAsync("bob", activityId);

// Custom predicate
var activity = await s2s.WaitForInboxActivityAsync("bob", 
    a => a.Content?.FirstOrDefault()?.Contains("hello") == true);
```

## Comparison with Old Tests

### Old Approach (_Old folder)
- Mixed mock objects and real HTTP
- Single server with simulated federation
- Complex test setup
- Hard to verify delivery timing

### New Approach
- Multiple real server instances
- Real HTTP communication between servers
- Simplified test setup
- Explicit polling for async operations
- Clearer separation of C2S and S2S concerns

## Running Tests

```bash
dotnet test
```

Or run specific tests:

```bash
dotnet test --filter "FullyQualifiedName~ClientToServerTests"
dotnet test --filter "FullyQualifiedName~ServerToServerTests"
```

## Next Steps

The framework is designed to be extended with:

- Collection handling helpers
- More activity type builders (Announce, Delete, Update, etc.)
- Follower/following relationship helpers
- Signature verification testing
- Rate limiting tests
- Error scenario testing
- WebFinger integration

## Migration from Old Tests

The `Broca.ActivityPub.IntegrationTests_Old` folder contains the previous test suite. You can:

1. Reference patterns from old tests
2. Copy specific test scenarios
3. Adapt to the new multi-server approach
4. Eventually delete once migration is complete
