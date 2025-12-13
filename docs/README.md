# Broca ActivityPub Library Documentation

Broca is a modular .NET library for building ActivityPub-enabled applications. It provides client, server, and web client capabilities with a clean, fluent API.

## Quick Start

### Client Setup

**Anonymous Client** (read-only access):
```csharp
services.AddActivityPubClient();

var client = serviceProvider.GetRequiredService<IActivityPubClient>();
var actor = await client.GetActorAsync(new Uri("https://example.com/users/alice"));
```

**Authenticated Client** (full federation):
```csharp
services.AddActivityPubClientAuthenticated(
    actorId: "https://myserver.com/users/john",
    privateKeyPem: yourPrivateKey,
    publicKeyId: "https://myserver.com/users/john#main-key"
);

var client = serviceProvider.GetRequiredService<IActivityPubClient>();
```

### Creating Activities

Use the fluent activity builder to create and post activities:

```csharp
// Create a public note
var note = client.CreateActivityBuilder()
    .CreateNote("Hello, fediverse!")
    .ToPublic()
    .Build();

await client.PostToOutboxAsync(note);

// Follow an actor
var follow = client.CreateActivityBuilder()
    .Follow("https://example.com/users/alice");
    
await client.PostToOutboxAsync(follow);

// Like a post
var like = client.CreateActivityBuilder()
    .Like("https://example.com/notes/123");
    
await client.PostToOutboxAsync(like);
```

### Server Setup

Add ActivityPub server capabilities to your ASP.NET Core application:

```csharp
// In Program.cs
builder.Services.AddActivityPubServer(options =>
{
    options.BaseUrl = "https://myserver.com";
});

// Controllers for inbox, outbox, and WebFinger are automatically registered
```

## Core Components

### Client (`Broca.ActivityPub.Client`)

- **IActivityPubClient** - Main client interface for all ActivityPub operations
- **IActivityBuilder** - Fluent API for creating activities
- **HTTP Signature Support** - Automatic signing for authenticated requests
- **WebFinger Resolution** - Resolve actors by @user@domain.tld aliases

### Server (`Broca.ActivityPub.Server`)

- **Actor Management** - Create and manage ActivityPub actors
- **Inbox/Outbox** - Handle incoming and outgoing activities
- **WebFinger** - Discover actors on your server
- **System Identity** - Server-level actor for authenticated federation (sys@domain)

### Persistence (`Broca.ActivityPub.Persistence`)

Abstraction layer for storing ActivityPub data:
- **IActorRepository** - Actor storage
- **IActivityRepository** - Activity storage
- **IFollowerRepository** - Follow relationships

Implementations:
- In-memory (for testing/development)
- Entity Framework Core support

### Types (`Broca.ActivityPub.Core`)

Uses the `KristofferStrube.ActivityStreams` package for standard-compliant types:
- 30+ activity types (Create, Follow, Like, Announce, etc.)
- Actor types (Person, Group, Service, Application)
- Object types (Note, Article, Image, Video, etc.)
- Strongly typed, JSON-LD compatible

## Key Features

### Activity Builder
Create activities with proper identity anchoring:
- Automatic actor attribution
- Automatic ID generation
- Fluent audience targeting (ToPublic, ToFollowers, To, Cc)
- Support for mentions, replies, and threading

### Dual-Mode Client
- **Anonymous**: Read public content without authentication
- **Authenticated**: Full federation capabilities with HTTP signatures

### System Identity
Server has its own identity (`sys@domain`) for federated operations:
- Sending follow requests
- Delivering activities on behalf of the server
- Administrative actions

### Modular Architecture
Each component is independently usable:
- Use just the client for federation
- Use just the server for hosting actors
- Use persistence abstractions with your own storage
- Mix and match as needed

## Examples

See the `/samples` directory for complete working examples:
- **Broca.Sample.MinimalApi** - Minimal API setup
- **Broca.Sample.WebApi** - Full Web API with controllers
- **Broca.Sample.BlazorApp** - Blazor with ActivityPub integration

## Dependencies

- **KristofferStrube.ActivityStreams** (v0.2.4) - ActivityPub/ActivityStreams types
- ASP.NET Core 8.0+
- System.Text.Json for serialization
