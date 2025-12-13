# Admin Operations with Bearer Token Authentication

## Overview

Broca ActivityPub Server now supports **bearer token authentication** for administrative operations, providing a simpler alternative to HTTP signature-based authentication. This approach:

- ✅ **Maintains ActivityPub compliance** - Uses standard ActivityPub endpoints and message formats
- ✅ **Simplifies client implementation** - No need for HTTP signature generation
- ✅ **Advertises capability via custom JSON-LD context** - Other implementations can discover this feature
- ✅ **Backward compatible** - HTTP signature authentication still works for federated admin operations

## Custom JSON-LD Extension

Broca uses a custom namespace (`broca:`) to advertise administrative capabilities in a spec-compliant way:

```json
{
  "@context": [
    "https://www.w3.org/ns/activitystreams",
    "https://w3id.org/security/v1",
    {
      "broca": "https://broca-activitypub.org/ns#"
    }
  ],
  "id": "https://yourdomain.com/users/sys",
  "type": "Application",
  "preferredUsername": "sys",
  "name": "System Actor",
  "broca:adminOperations": {
    "enabled": true,
    "authenticationMethods": ["bearer"],
    "description": "This server supports administrative operations via ActivityPub protocol with bearer token authentication",
    "endpoint": "https://yourdomain.com/users/sys/inbox"
  }
}
```

## Configuration

### 1. Enable Admin Operations

In `appsettings.json`:

```json
{
  "ActivityPub": {
    "BaseUrl": "https://yourdomain.com",
    "SystemActorUsername": "sys",
    "EnableAdminOperations": true,
    "AdminApiToken": "your-secure-token-here"
  }
}
```

### 2. Generate a Secure Token

```bash
# Generate a random 32-byte token
openssl rand -base64 32
```

## Authentication Flow

When a request is made to the system actor's inbox:

1. **Bearer Token Check**: Server checks for `Authorization: Bearer <token>` header
2. **Token Validation**: If token matches, authentication succeeds
3. **HTTP Signature Bypass**: Valid bearer token bypasses HTTP signature requirement
4. **Admin Operation Processing**: Activity is processed as an admin operation
5. **No Actor Validation**: Bearer token authenticates the request, so actor ID in activity is optional

### Without Bearer Token (Traditional)

```http
POST /users/sys/inbox HTTP/1.1
Host: yourdomain.com
Content-Type: application/activity+json
Signature: keyId="https://example.com/users/admin#main-key",...
Digest: SHA-256=...

{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Create",
  "actor": "https://example.com/users/admin",
  "object": {
    "type": "Person",
    "preferredUsername": "alice"
  }
}
```

Requirements:
- ✅ HTTP signature validation
- ✅ Actor must be in `AuthorizedAdminActors`
- ✅ Requires private key for signing

### With Bearer Token (Simplified)

```http
POST /users/sys/inbox HTTP/1.1
Host: yourdomain.com
Content-Type: application/activity+json
Authorization: Bearer your-secure-token-here

{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Create",
  "object": {
    "type": "Person",
    "preferredUsername": "alice"
  }
}
```

Requirements:
- ✅ Valid bearer token
- ❌ No HTTP signature needed
- ❌ No actor ID validation needed
- ❌ No private key management

## Usage Examples

### Create User

```bash
curl -X POST https://yourdomain.com/users/sys/inbox \
  -H "Authorization: Bearer your-token" \
  -H "Content-Type: application/activity+json" \
  -d '{
    "@context": "https://www.w3.org/ns/activitystreams",
    "type": "Create",
    "object": {
      "type": "Person",
      "preferredUsername": "alice",
      "name": "Alice Wonderland",
      "summary": "A curious explorer"
    }
  }'
```

### Update User

```bash
curl -X POST https://yourdomain.com/users/sys/inbox \
  -H "Authorization: Bearer your-token" \
  -H "Content-Type: application/activity+json" \
  -d '{
    "@context": "https://www.w3.org/ns/activitystreams",
    "type": "Update",
    "object": {
      "type": "Person",
      "preferredUsername": "alice",
      "name": "Alice Cooper",
      "summary": "Updated bio"
    }
  }'
```

### Delete User

```bash
curl -X POST https://yourdomain.com/users/sys/inbox \
  -H "Authorization: Bearer your-token" \
  -H "Content-Type: application/activity+json" \
  -d '{
    "@context": "https://www.w3.org/ns/activitystreams",
    "type": "Delete",
    "object": {
      "type": "Person",
      "id": "https://yourdomain.com/users/alice"
    }
  }'
```

## Client Implementation

### C# Example

```csharp
public class BrocaAdminClient
{
    private readonly HttpClient _http;
    private readonly string _bearerToken;
    private readonly string _systemInboxUrl;

    public BrocaAdminClient(string baseUrl, string bearerToken)
    {
        _http = new HttpClient();
        _bearerToken = bearerToken;
        _systemInboxUrl = $"{baseUrl}/users/sys/inbox";
    }

    public async Task<bool> CreateUserAsync(string username, string displayName, string bio = null)
    {
        var activity = new
        {
            context = "https://www.w3.org/ns/activitystreams",
            type = "Create",
            @object = new
            {
                type = "Person",
                preferredUsername = username,
                name = displayName,
                summary = bio
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, _systemInboxUrl)
        {
            Content = JsonContent.Create(activity)
        };
        
        request.Headers.Add("Authorization", $"Bearer {_bearerToken}");
        request.Headers.Add("Accept", "application/activity+json");

        var response = await _http.SendAsync(request);
        return response.IsSuccessStatusCode;
    }
}
```

### JavaScript/TypeScript Example

```typescript
class BrocaAdminClient {
  constructor(
    private baseUrl: string,
    private bearerToken: string
  ) {}

  async createUser(username: string, displayName: string, bio?: string): Promise<boolean> {
    const activity = {
      '@context': 'https://www.w3.org/ns/activitystreams',
      type: 'Create',
      object: {
        type: 'Person',
        preferredUsername: username,
        name: displayName,
        summary: bio
      }
    };

    const response = await fetch(`${this.baseUrl}/users/sys/inbox`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${this.bearerToken}`,
        'Content-Type': 'application/activity+json',
        'Accept': 'application/activity+json'
      },
      body: JSON.stringify(activity)
    });

    return response.ok;
  }
}
```

## Security Considerations

### 1. Token Storage

- **Never commit tokens** to version control
- **Use environment variables** or secure configuration management
- **Rotate tokens regularly** to minimize exposure risk

### 2. Transport Security

- **Always use HTTPS** in production
- Bearer tokens are sent in plaintext headers
- TLS encryption is essential

### 3. Token Strength

```bash
# Generate a cryptographically secure token
openssl rand -base64 32

# Or use a GUID/UUID
uuidgen
```

### 4. Monitoring

- **Log all admin operations** for audit trails
- **Monitor for unauthorized access attempts**
- **Set up alerts** for suspicious activity patterns

### 5. Principle of Least Privilege

- Use separate tokens for different admin clients if possible
- Consider implementing token scopes (future enhancement)
- Revoke tokens immediately when compromised

## Advantages Over HTTP Signatures

| Feature | Bearer Token | HTTP Signature |
|---------|-------------|----------------|
| **Implementation Complexity** | Simple | Complex |
| **Key Management** | Single shared secret | RSA key pairs per actor |
| **Request Signing** | Not required | Required for each request |
| **Clock Synchronization** | Not critical | Critical for date headers |
| **Client Libraries** | Standard HTTP clients | Specialized crypto libraries |
| **Debugging** | Easy (just a header) | Difficult (signature generation) |
| **Federation** | Internal only | Works across servers |
| **Revocation** | Change one token | Revoke actor authorization |

## When to Use Each Method

### Use Bearer Token Authentication When:

- ✅ Building internal admin tools
- ✅ Simplifying client implementation
- ✅ Managing users from trusted environments
- ✅ You control both client and server

### Use HTTP Signature Authentication When:

- ✅ Federating admin operations across servers
- ✅ You need cryptographic proof of sender identity
- ✅ Multiple external parties need admin access
- ✅ Compliance requires non-repudiation

## Discovery

Clients can discover bearer token support by fetching the system actor profile:

```bash
curl https://yourdomain.com/users/sys \
  -H "Accept: application/activity+json"
```

Look for the `broca:adminOperations` extension:

```json
{
  "broca:adminOperations": {
    "enabled": true,
    "authenticationMethods": ["bearer"],
    "endpoint": "https://yourdomain.com/users/sys/inbox"
  }
}
```

## Relationship to ActivityPub Spec

This implementation is **fully compliant** with the ActivityPub specification:

1. **Standard endpoints** - Uses the canonical inbox endpoint
2. **Standard activities** - Uses Create, Update, Delete from ActivityStreams vocabulary
3. **Custom extension** - The `broca:` namespace follows JSON-LD extension patterns
4. **HTTP authentication** - Bearer tokens are standard HTTP authentication (RFC 6750)
5. **No protocol modifications** - ActivityPub message format is unchanged

The ActivityPub spec explicitly supports:
- Custom authentication mechanisms (§7.1 Authentication and Authorization)
- Extension vocabularies via JSON-LD contexts (§3 Objects)
- Implementation-specific features (§1.3 Conformance)

## Migration Path

Existing implementations using HTTP signatures continue to work. You can:

1. **Add bearer token support** without removing HTTP signature support
2. **Use both methods** simultaneously for different clients
3. **Gradually migrate** internal tools to bearer tokens
4. **Keep HTTP signatures** for federated admin operations

## Related Documentation

- [Admin Back Channel](./ADMIN_BACK_CHANNEL.md) - Overview of admin operations
- [Admin Token Access](./ADMIN_TOKEN_ACCESS.md) - Token-based actor retrieval
- [System Actor Configuration](./SYSTEM_ACTOR_CONFIGURATION.md) - System actor setup
