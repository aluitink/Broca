# Identity Provider System

The Broca ActivityPub Server includes a flexible identity provider system that makes it easy to expose user identities via ActivityPub. This allows you to integrate ActivityPub into existing applications (blogs, CMSs, social platforms) with minimal code.

## Overview

The identity provider system allows you to:
- **Plug into existing systems** - Connect your existing user database, CMS, or authentication system
- **Auto-generate ActivityPub actors** - The system automatically creates and manages actors based on your identity data
- **Serve all endpoints** - WebFinger, Actor, Inbox, Outbox, and collections are automatically handled
- **Simple configuration** - For basic use cases, just add configuration to `appsettings.json`
- **Extensible** - Implement `IIdentityProvider` for custom identity sources

## Quick Start: Single User (Simple Identity Provider)

Perfect for personal blogs, portfolio sites, or single-user instances.

### 1. Configure in appsettings.json

```json
{
  "ActivityPub": {
    "BaseUrl": "https://myblog.com",
    "PrimaryDomain": "myblog.com",
    "ServerName": "My Personal Blog",
    "SystemActorUsername": "sys"
  },
  "IdentityProvider": {
    "SimpleIdentity": {
      "Username": "alice",
      "DisplayName": "Alice Smith",
      "Summary": "Software developer, writer, and coffee enthusiast. Sharing thoughts on tech, books, and life.",
      "AvatarUrl": "https://myblog.com/images/avatar.jpg",
      "HeaderUrl": "https://myblog.com/images/header.jpg",
      "ActorType": "Person",
      "IsBot": false,
      "IsLocked": false,
      "IsDiscoverable": true,
      "Fields": {
        "Website": "https://myblog.com",
        "GitHub": "https://github.com/alice"
      }
    }
  }
}
```

### 2. Register in Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add Broca server
builder.Services.AddBrocaServer(builder.Configuration);

// Add simple identity provider
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

### 3. That's it!

Your identity is now available at:
- **WebFinger**: `https://myblog.com/.well-known/webfinger?resource=acct:alice@myblog.com`
- **Actor**: `https://myblog.com/users/alice`
- **Profile**: Can be followed from Mastodon as `@alice@myblog.com`

## Advanced: Custom Identity Provider

For multi-user applications or integration with existing systems.

### 1. Implement IIdentityProvider

```csharp
using Broca.ActivityPub.Core.Interfaces;

public class DatabaseIdentityProvider : IIdentityProvider
{
    private readonly MyDbContext _dbContext;
    private readonly ILogger<DatabaseIdentityProvider> _logger;

    public DatabaseIdentityProvider(MyDbContext dbContext, ILogger<DatabaseIdentityProvider> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<string>> GetUsernamesAsync(CancellationToken cancellationToken = default)
    {
        // Return all users that should be available via ActivityPub
        return await _dbContext.Users
            .Where(u => u.IsPublic)
            .Select(u => u.Username)
            .ToListAsync(cancellationToken);
    }

    public async Task<IdentityDetails?> GetIdentityDetailsAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user == null)
            return null;

        return new IdentityDetails
        {
            Username = user.Username,
            DisplayName = user.DisplayName,
            Summary = user.Bio,
            AvatarUrl = user.AvatarUrl,
            HeaderUrl = user.HeaderImageUrl,
            ActorType = ActorType.Person,
            IsBot = user.IsBot,
            IsLocked = user.RequiresFollowApproval,
            IsDiscoverable = user.IsPublic,
            Fields = new Dictionary<string, string>
            {
                { "Website", user.Website },
                { "Location", user.Location }
            }
        };
    }

    public async Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AnyAsync(u => u.Username == username && u.IsPublic, cancellationToken);
    }
}
```

### 2. Register your provider

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBrocaServer(builder.Configuration);

// Register your custom provider
builder.Services.AddIdentityProvider<DatabaseIdentityProvider>();

var app = builder.Build();

// Initialize all identities
var identityService = app.Services.GetRequiredService<IdentityProviderService>();
await identityService.InitializeIdentitiesAsync();

app.MapControllers();
app.Run();
```

## Using Pre-existing Keys

If you already have RSA key pairs (e.g., from migrating from another ActivityPub server):

### Option 1: Configuration (Simple Identity)

```json
{
  "IdentityProvider": {
    "SimpleIdentity": {
      "Username": "alice",
      "DisplayName": "Alice Smith",
      "PrivateKeyPath": "/path/to/private-key.pem",
      "PublicKeyPath": "/path/to/public-key.pem"
    }
  }
}
```

### Option 2: Custom Provider

```csharp
public async Task<IdentityDetails?> GetIdentityDetailsAsync(string username, CancellationToken cancellationToken)
{
    var user = await GetUserFromDatabase(username);
    if (user == null) return null;

    return new IdentityDetails
    {
        Username = user.Username,
        DisplayName = user.DisplayName,
        // ... other properties ...
        Keys = new KeyPair
        {
            PublicKey = user.PublicKeyPem,
            PrivateKey = user.PrivateKeyPem
        }
    };
}
```

## How It Works

1. **Startup**: When your app starts, `IdentityProviderService.InitializeIdentitiesAsync()` is called
2. **Actor Creation**: For each username from your provider, an ActivityPub Actor is created and stored
3. **Key Generation**: If keys aren't provided, RSA 2048-bit key pairs are automatically generated
4. **Endpoint Serving**: The ActorController and WebFingerController automatically serve the actors
5. **Lazy Creation**: If an identity provider is registered, actors can be created on-demand when first accessed

## Identity Details

The `IdentityDetails` class supports:

| Property | Description | Required |
|----------|-------------|----------|
| `Username` | Username without domain (e.g., "alice") | ✅ Yes |
| `DisplayName` | Full name or display name | No |
| `Summary` | Bio/about text (supports HTML) | No |
| `AvatarUrl` | Profile image URL | No |
| `HeaderUrl` | Banner/header image URL | No |
| `ActorType` | Person, Organization, Service, Application, Group | No (default: Person) |
| `IsBot` | Mark as bot account | No (default: false) |
| `IsLocked` | Requires follow approval | No (default: false) |
| `IsDiscoverable` | Appear in directories | No (default: true) |
| `Keys` | Pre-existing RSA key pair | No (auto-generated if not provided) |
| `Fields` | Custom profile fields | No |

## Use Cases

### Personal Blog
Use `SimpleIdentityProvider` with configuration to make your blog followable on Mastodon.

### Multi-user CMS
Implement `IIdentityProvider` to expose all authors/contributors as ActivityPub actors.

### Forum/Community
Create actors for forum users, allowing them to be followed and receive notifications.

### Newsletter
Expose your newsletter as a followable actor - new posts become federated updates.

### Portfolio Site
Make your professional portfolio discoverable and followable across the fediverse.

## Security Considerations

1. **Private Keys**: By default, private keys are stored in the actor's `ExtensionData`. In production, consider:
   - Using Azure Key Vault or AWS KMS
   - Implementing a custom `IActorRepository` with encrypted storage
   - File-based storage with proper permissions

2. **User Privacy**: Only expose users who have opted in to federation

3. **Rate Limiting**: Consider rate limiting on WebFinger and Actor endpoints

## Implementation Architecture

### Core Components

**`IIdentityProvider` Interface** (`Core/Interfaces/IIdentityProvider.cs`)
- Abstraction for providing identity data
- Three key methods:
  - `GetUsernamesAsync()` - Returns all usernames to expose
  - `GetIdentityDetailsAsync(username)` - Returns details for a specific user
  - `ExistsAsync(username)` - Checks if a username exists

**`SimpleIdentityProvider`** (`Server/Services/SimpleIdentityProvider.cs`)
- Basic implementation using configuration
- Perfect for single-user instances
- Loads identity from appsettings.json
- Supports loading pre-existing keys from files

**`IdentityProviderService`** (`Server/Services/IdentityProviderService.cs`)
- Core service that bridges identity providers with ActivityPub
- Auto-generates actors from identity details
- Creates RSA key pairs if not provided
- Manages actor lifecycle (create, cache, retrieve)
- Thread-safe initialization

**Updated `ActorController`**
- Checks identity provider when actor not found in repository
- Enables lazy creation of actors on-demand
- Seamless fallback to repository-based actors

### Key Features

1. **Simple for Basic Use Cases** - Config-based single user setup
2. **Extensible for Complex Scenarios** - Custom provider interface for databases, APIs, etc.
3. **Automatic Actor Management** - Actors created automatically from identity details
4. **Lazy Creation Support** - Actors can be created on-demand when first accessed
5. **Backward Compatible** - Identity provider is opt-in, no breaking changes

## Next Steps

- See [samples/Broca.Sample.WebApi](../samples/Broca.Sample.WebApi) for a complete example
- Check [IDENTITY_EXAMPLES.md](../samples/IDENTITY_EXAMPLES.md) for code examples
- Read [SERVER_IMPLEMENTATION.md](./SERVER_IMPLEMENTATION.md) for more server details
- Check [CLIENT.md](./CLIENT.md) for client-side integration
