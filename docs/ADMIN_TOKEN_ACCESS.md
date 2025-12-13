# Admin Token Access for Private Keys

## Overview

The Broca ActivityPub Server now supports returning actor private keys when a valid admin API token is provided. This allows administrative interfaces (like the Blazor sample app) to retrieve private keys for signing activities on behalf of users.

## Security Features

- **Token-based authentication**: Uses Bearer token authentication
- **Constant-time comparison**: Prevents timing attacks when validating tokens
- **Configurable**: Token can be set per environment
- **Opt-in**: Private keys are only returned when a valid token is provided

## Configuration

### 1. Generate a Secure Token

Generate a strong random token:

```bash
# Generate a 32-byte base64-encoded token
openssl rand -base64 32
```

### 2. Add to Configuration

Add the token to your `appsettings.json`:

```json
{
  "ActivityPub": {
    "BaseUrl": "https://yourdomain.com",
    "SystemActorUsername": "sys",
    "EnableAdminOperations": true,
    "AdminApiToken": "your-secure-random-token-here"
  }
}
```

**⚠️ Security Warning:** Keep this token secret! Anyone with this token can retrieve private keys for all actors.

## Usage

### Retrieving Actor with Private Key

Make a request to the actor endpoint with the admin token:

```bash
curl https://yourdomain.com/users/alice \
  -H "Authorization: Bearer your-secure-random-token-here" \
  -H "Accept: application/activity+json"
```

**Response with valid token:**

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "id": "https://yourdomain.com/users/alice",
  "type": "Person",
  "preferredUsername": "alice",
  "inbox": "https://yourdomain.com/users/alice/inbox",
  "outbox": "https://yourdomain.com/users/alice/outbox",
  "publicKey": {
    "id": "https://yourdomain.com/users/alice#main-key",
    "owner": "https://yourdomain.com/users/alice",
    "publicKeyPem": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----"
  },
  "privateKeyPem": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----"
}
```

**Response without token or with invalid token:**

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "id": "https://yourdomain.com/users/alice",
  "type": "Person",
  "preferredUsername": "alice",
  "inbox": "https://yourdomain.com/users/alice/inbox",
  "outbox": "https://yourdomain.com/users/alice/outbox",
  "publicKey": {
    "id": "https://yourdomain.com/users/alice#main-key",
    "owner": "https://yourdomain.com/users/alice",
    "publicKeyPem": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----"
  }
}
```

## Implementation Details

### How It Works

1. **Configuration Check**: The server checks if `AdminApiToken` is configured
2. **Header Extraction**: Extracts the `Authorization` header from the request
3. **Token Validation**: Validates the Bearer token using constant-time comparison
4. **Response Filtering**: 
   - If valid token: Returns actor with `privateKeyPem` in `ExtensionData`
   - If invalid/missing token: Removes `privateKeyPem` from response

### Code Location

- **Configuration**: `src/Broca.ActivityPub.Core/Models/ActivityPubServerOptions.cs`
- **Implementation**: `src/Broca.ActivityPub.Server/Controllers/ActorController.cs`

### Security Considerations

1. **Always use HTTPS** in production to protect the token in transit
2. **Rotate tokens regularly** to minimize exposure risk
3. **Use environment variables** or secure configuration management (e.g., Azure Key Vault)
4. **Audit access logs** to monitor token usage
5. **Never commit tokens** to version control

## Example: Blazor App Integration

Update your Blazor app's configuration:

```json
{
  "ActivityPubApi": {
    "BaseUrl": "http://localhost:5050",
    "AdminApiToken": "your-secure-random-token-here"
  }
}
```

Use in your service:

```csharp
public class AdminService
{
    private readonly HttpClient _http;
    private readonly string _adminToken;
    
    public AdminService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _adminToken = config["ActivityPubApi:AdminApiToken"];
    }
    
    public async Task<Actor> GetActorWithPrivateKeyAsync(string username)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/users/{username}");
        request.Headers.Add("Authorization", $"Bearer {_adminToken}");
        request.Headers.Add("Accept", "application/activity+json");
        
        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<Actor>();
    }
}
```

## Compliance with ActivityPub Spec

This feature **maintains full ActivityPub spec compliance**:

- ✅ **Standard endpoints**: Uses the standard `/users/{username}` actor endpoint
- ✅ **No protocol changes**: HTTP authentication is standard and transparent to federation
- ✅ **Optional extension**: Private key is stored in `ExtensionData`, which is spec-compliant
- ✅ **Selective disclosure**: Private keys are only returned to authorized clients

The ActivityPub specification explicitly supports custom authentication mechanisms and extension data, making this approach fully compliant.

## Related Documentation

- [Admin Back Channel](./ADMIN_BACK_CHANNEL.md) - Administrative operations via ActivityPub
- [System Actor Configuration](./SYSTEM_ACTOR_CONFIGURATION.md) - System actor setup
- [Identity Provider](./IDENTITY_PROVIDER.md) - Identity provider integration
