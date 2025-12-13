# Identity Provider Quick Reference

## Simple Setup (Single User)

Perfect for blogs, portfolios, personal sites.

### appsettings.json
```json
{
  "ActivityPub": {
    "BaseUrl": "https://example.com",
    "PrimaryDomain": "example.com"
  },
  "IdentityProvider": {
    "SimpleIdentity": {
      "Username": "alice",
      "DisplayName": "Alice Smith",
      "Summary": "Software developer and writer"
    }
  }
}
```

### Program.cs
```csharp
builder.Services.AddBrocaServer(builder.Configuration);
builder.Services.AddSimpleIdentityProvider(builder.Configuration);

var app = builder.Build();

var identityService = app.Services.GetService<IdentityProviderService>();
if (identityService != null)
{
    await identityService.InitializeIdentitiesAsync();
}

app.MapControllers();
app.Run();
```

### Result
- WebFinger: `/.well-known/webfinger?resource=acct:alice@example.com`
- Actor: `/users/alice`
- Followable as: `@alice@example.com`

---

## Custom Provider (Multi-User)

For CMSs, forums, multi-user blogs.

### Implementation
```csharp
public class MyIdentityProvider : IIdentityProvider
{
    private readonly MyDbContext _db;

    public MyIdentityProvider(MyDbContext db) => _db = db;

    public async Task<IEnumerable<string>> GetUsernamesAsync(CancellationToken ct = default)
    {
        return await _db.Users
            .Where(u => u.IsPublic)
            .Select(u => u.Username)
            .ToListAsync(ct);
    }

    public async Task<IdentityDetails?> GetIdentityDetailsAsync(string username, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user == null) return null;

        return new IdentityDetails
        {
            Username = user.Username,
            DisplayName = user.DisplayName,
            Summary = user.Bio,
            AvatarUrl = user.AvatarUrl,
            ActorType = ActorType.Person
        };
    }

    public async Task<bool> ExistsAsync(string username, CancellationToken ct = default)
    {
        return await _db.Users.AnyAsync(u => u.Username == username, ct);
    }
}
```

### Registration
```csharp
builder.Services.AddBrocaServer(builder.Configuration);
builder.Services.AddIdentityProvider<MyIdentityProvider>();
```

---

## Configuration Options

### IdentityDetails Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Username` | string | ✅ Yes | - | Username without domain |
| `DisplayName` | string | No | null | Full/display name |
| `Summary` | string | No | null | Bio text (HTML supported) |
| `AvatarUrl` | string | No | null | Profile image URL |
| `HeaderUrl` | string | No | null | Header/banner URL |
| `ActorType` | enum | No | Person | Person, Organization, Service, Application, Group |
| `IsBot` | bool | No | false | Mark as bot account |
| `IsLocked` | bool | No | false | Require follow approval |
| `IsDiscoverable` | bool | No | true | Show in directories |
| `Keys` | KeyPair | No | null | Pre-existing RSA keys (auto-generated if null) |
| `Fields` | Dictionary | No | null | Custom profile fields |

### ActivityPubServerOptions

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `BaseUrl` | string | ✅ Yes | - | Full base URL (https://example.com) |
| `PrimaryDomain` | string | ✅ Yes | - | Domain name (example.com) |
| `ServerName` | string | No | "Broca Server" | Display name |
| `SystemActorUsername` | string | No | "sys" | System actor username |
| `RoutePrefix` | string | No | "" | Route prefix (e.g., "ap") |

---

## Common Patterns

### Personal Blog
```json
{
  "IdentityProvider": {
    "SimpleIdentity": {
      "Username": "author",
      "DisplayName": "Jane Doe",
      "Summary": "Tech writer and blogger",
      "Fields": {
        "Website": "https://janedoe.com"
      }
    }
  }
}
```

### Newsletter
```json
{
  "IdentityProvider": {
    "SimpleIdentity": {
      "Username": "newsletter",
      "DisplayName": "Weekly Tech Digest",
      "ActorType": "Service",
      "Summary": "Weekly newsletter covering tech news"
    }
  }
}
```

### Organization
```json
{
  "IdentityProvider": {
    "SimpleIdentity": {
      "Username": "company",
      "DisplayName": "Acme Corp",
      "ActorType": "Organization",
      "Summary": "Official updates from Acme Corp"
    }
  }
}
```

### Bot Account
```json
{
  "IdentityProvider": {
    "SimpleIdentity": {
      "Username": "bot",
      "DisplayName": "Status Bot",
      "ActorType": "Service",
      "IsBot": true,
      "Summary": "Automated status updates"
    }
  }
}
```

---

## Testing

### Local Development
```bash
# Update appsettings.Development.json
{
  "ActivityPub": {
    "BaseUrl": "http://localhost:5000",
    "PrimaryDomain": "localhost"
  }
}

# Run
dotnet run

# Test WebFinger
curl "http://localhost:5000/.well-known/webfinger?resource=acct:alice@localhost"

# Test Actor
curl -H "Accept: application/activity+json" http://localhost:5000/users/alice
```

### Production
Ensure HTTPS and proper DNS:
```bash
# Test from Mastodon search
@alice@yourdomain.com

# Or use curl
curl "https://yourdomain.com/.well-known/webfinger?resource=acct:alice@yourdomain.com"
```

---

## Troubleshooting

**Actor not found?**
- Check `InitializeIdentitiesAsync()` is called on startup
- Verify configuration is loaded correctly
- Check logs for initialization errors

**Can't follow from Mastodon?**
- Ensure BaseUrl uses HTTPS in production
- Verify DNS is correctly configured
- Check WebFinger endpoint returns correct data
- Ensure HTTP Signatures are enabled

**Keys not persisting?**
- Keys are stored in actor's ExtensionData by default
- For production, implement custom storage or use key files
- Check IActorRepository implementation

---

## See Also

- [Full Documentation](./IDENTITY_PROVIDER.md)
- [Sample Implementations](../samples/IDENTITY_EXAMPLES.md)
- [Server Guide](./SERVER_IMPLEMENTATION.md)
