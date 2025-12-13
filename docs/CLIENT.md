# Broca.ActivityPub.Client

A comprehensive ActivityPub client library for .NET that supports both anonymous browsing and authenticated interactions with the fediverse.

## Features

- **Anonymous Mode**: Browse public ActivityPub content without authentication
- **Authenticated Mode**: Sign requests with your actor's private key to access protected resources and post activities
- **HTTP Signatures**: Full support for HTTP Signatures (required by Mastodon and other servers)
- **WebFinger**: Resolve user aliases (@user@domain.tld) to ActivityPub actor URIs
- **Collection Pagination**: Automatically handle paginated collections
- **Caching**: Built-in caching support for improved performance
- **Type-Safe**: Strongly-typed API for type safety and IntelliSense support

## Installation

```bash
dotnet add package Broca.ActivityPub.Client
```

## Quick Start

### Anonymous Mode

Use anonymous mode to browse public ActivityPub content without authentication:

```csharp
using Broca.ActivityPub.Client.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register the ActivityPub client in anonymous mode
services.AddActivityPubClient();

var serviceProvider = services.BuildServiceProvider();
var client = serviceProvider.GetRequiredService<IActivityPubClient>();

// Fetch a public actor
var actor = await client.GetActorAsync(new Uri("https://mastodon.social/@Gargron"));
Console.WriteLine($"Actor: {actor}");

// Resolve an actor by alias
var resolvedActor = await client.GetActorByAliasAsync("@Gargron@mastodon.social");
Console.WriteLine($"Resolved: {resolvedActor}");

// Fetch any ActivityPub object
var note = await client.GetAsync<object>(new Uri("https://mastodon.social/users/Gargron/statuses/123456"));
```

### Authenticated Mode

Use authenticated mode when you need to sign requests with your actor's credentials:

```csharp
using Broca.ActivityPub.Client.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Your actor credentials
var actorId = "https://example.com/users/alice";
var privateKeyPem = @"-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC...
-----END PRIVATE KEY-----";
var publicKeyId = "https://example.com/users/alice#main-key";

// Register the ActivityPub client in authenticated mode
services.AddActivityPubClientAuthenticated(actorId, privateKeyPem, publicKeyId);

var serviceProvider = services.BuildServiceProvider();
var client = serviceProvider.GetRequiredService<IActivityPubClient>();

// Get your own actor profile
var self = await client.GetSelfAsync();
Console.WriteLine($"Authenticated as: {self}");

// Post an activity (requires authentication)
var activity = new
{
    type = "Create",
    actor = actorId,
    @object = new
    {
        type = "Note",
        content = "Hello from Broca!",
        to = new[] { "https://www.w3.org/ns/activitystreams#Public" }
    }
};

var inboxUri = new Uri("https://mastodon.social/inbox");
var response = await client.PostAsync(inboxUri, activity);
Console.WriteLine($"Status: {response.StatusCode}");
```

## Configuration Options

Customize the client behavior with `ActivityPubClientOptions`:

```csharp
services.AddActivityPubClient(options =>
{
    options.UserAgent = "MyApp/1.0";
    options.TimeoutSeconds = 60;
    options.EnableCaching = true;
    options.CacheExpirationMinutes = 30;
});
```

For authenticated mode with custom options:

```csharp
services.AddActivityPubClientAuthenticated(
    actorId: "https://example.com/users/alice",
    privateKeyPem: privateKey,
    publicKeyId: "https://example.com/users/alice#main-key",
    configureOptions: options =>
    {
        options.UserAgent = "MyApp/1.0";
        options.EnableCaching = false;
    });
```

## Working with Collections

The client automatically handles paginated collections:

```csharp
// Fetch all items from a collection (with automatic pagination)
var collectionUri = new Uri("https://mastodon.social/users/Gargron/followers");

await foreach (var follower in client.GetCollectionAsync<object>(collectionUri))
{
    Console.WriteLine($"Follower: {follower}");
}

// Limit the number of items
await foreach (var follower in client.GetCollectionAsync<object>(collectionUri, limit: 10))
{
    Console.WriteLine($"Follower: {follower}");
}
```

## API Reference

### IActivityPubClient

#### Properties

- `ActorId`: Gets the current authenticated actor ID, or null if anonymous

#### Methods

- `GetSelfAsync()`: Gets or fetches the authenticated actor (null in anonymous mode)
- `GetActorAsync(Uri actorUri)`: Fetches an actor by their URI
- `GetActorByAliasAsync(string alias)`: Resolves an actor alias to an actor object
- `GetAsync<T>(Uri uri, bool useCache)`: Fetches any ActivityPub object
- `PostAsync<T>(Uri uri, T obj)`: Posts an object (requires authentication)
- `GetCollectionAsync<T>(Uri collectionUri, int? limit)`: Fetches a paginated collection

## How It Works

### Anonymous Mode

In anonymous mode, the client makes simple HTTP GET requests without any signature headers. This works for accessing public content but won't allow you to:
- Access protected/private content
- Post activities
- Follow users
- Interact with content (like, share, etc.)

### Authenticated Mode

In authenticated mode, the client:
1. Signs all HTTP requests using HTTP Signatures (RSA-SHA256)
2. Includes required headers: `Date`, `Host`, `Digest` (for POST), etc.
3. Uses your actor's private key to create the signature
4. Includes the signature in the `Signature` header

This allows full interaction with ActivityPub servers, including Mastodon, Pleroma, and others.

### HTTP Signatures

The client implements HTTP Signatures as required by Mastodon:
- Signs the `(request-target)`, `host`, and `date` headers for GET requests
- Signs the `digest` header for POST/PUT requests with body
- Uses RSA-SHA256 for signing
- Supports both `Date` header and `(created)` pseudo-header (for browser contexts)

### Caching

The client includes a simple in-memory cache to reduce redundant requests:
- Caches fetched objects by URI
- Configurable expiration time (default: 60 minutes)
- Can be disabled via options
- Automatically cleans up expired entries

## Examples

### Fetch a Mastodon User

```csharp
var actor = await client.GetActorByAliasAsync("@Gargron@mastodon.social");
dynamic mastodonUser = actor;
Console.WriteLine($"Name: {mastodonUser.name}");
Console.WriteLine($"Summary: {mastodonUser.summary}");
```

### Follow a User (Authenticated)

```csharp
var followActivity = new
{
    type = "Follow",
    actor = actorId,
    @object = "https://mastodon.social/users/Gargron"
};

var targetInbox = new Uri("https://mastodon.social/users/Gargron/inbox");
var response = await client.PostAsync(targetInbox, followActivity);
```

### Fetch a User's Posts

```csharp
var outboxUri = new Uri("https://mastodon.social/users/Gargron/outbox");

await foreach (var activity in client.GetCollectionAsync<object>(outboxUri, limit: 20))
{
    dynamic post = activity;
    Console.WriteLine($"Type: {post.type}");
    Console.WriteLine($"Published: {post.published}");
}
```

## Requirements

- .NET 6.0 or later
- RSA key pair for authenticated mode (can be generated with OpenSSL)

### Generating Keys

Generate an RSA key pair for use with authenticated mode:

```bash
# Generate private key
openssl genpkey -algorithm RSA -out private_key.pem -pkeyopt rsa_keygen_bits:2048

# Extract public key
openssl rsa -pubout -in private_key.pem -out public_key.pem
```

## Related Projects

- [Broca.ActivityPub.Server](../Broca.ActivityPub.Server) - Server implementation for hosting ActivityPub actors
- [Broca.ActivityPub.Core](../Broca.ActivityPub.Core) - Core models and interfaces
- [Broca.ActivityPub.Persistence](../Broca.ActivityPub.Persistence) - Persistence abstractions

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
