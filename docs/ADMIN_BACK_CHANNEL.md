# Administrative Back Channel

## Overview

Broca ActivityPub Server supports **administrative operations via the ActivityPub protocol**. This provides a spec-compliant back-channel interface for managing users by posting properly signed activities to the system actor's inbox.

## What is This?

Instead of using a traditional REST API for administration, you can use ActivityPub activities to:
- **Create users** - Send a `Create` activity with an `Actor` object
- **Update users** - Send an `Update` activity with modified `Actor` properties
- **Delete users** - Send a `Delete` activity referencing an `Actor`

This approach:
- ✅ **Is spec-compliant** - Uses standard ActivityPub patterns (extensibility is built into the spec)
- ✅ **Leverages existing authentication** - Uses HTTP signatures already required for federation
- ✅ **Is fully auditable** - All operations are logged and stored in the activity repository
- ⚠️ **Is non-standard** - Other ActivityPub servers won't understand these operations (but that's fine for internal use)

## Configuration

### 1. Enable Admin Operations

In `appsettings.json`:

```json
{
  "ActivityPub": {
    "BaseUrl": "https://yourdomain.com",
    "SystemActorUsername": "sys",
    "EnableAdminOperations": true,
    "AuthorizedAdminActors": [
      "https://yourdomain.com/users/sys",
      "https://trusted-domain.com/users/admin"
    ]
  }
}
```

**Configuration Options:**

- `EnableAdminOperations` (bool) - Set to `true` to enable the admin back channel. Default: `false`
- `AuthorizedAdminActors` (string[]) - List of actor IDs permitted to perform admin operations. The system actor is always authorized.

### 2. Security Requirements

For an admin operation to succeed, **all** of the following must be true:

1. ✅ The activity is sent to the **system actor's inbox** (e.g., `/users/sys/inbox`)
2. ✅ The request has a **valid HTTP signature** (`RequireHttpSignatures` must be enabled)
3. ✅ The signature belongs to an **authorized admin actor** (in `AuthorizedAdminActors` or the system actor itself)
4. ✅ The activity is properly formatted according to ActivityPub spec

## Operations

### Create Actor

Creates a new user account.

**Activity Structure:**

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Create",
  "id": "https://yourdomain.com/activities/create-alice-001",
  "actor": "https://yourdomain.com/users/sys",
  "to": ["https://yourdomain.com/users/sys"],
  "object": {
    "type": "Person",
    "preferredUsername": "alice",
    "name": "Alice Wonderland",
    "summary": "Curious explorer of the fediverse"
  }
}
```

**What Happens:**

1. Server validates the signature and authorization
2. Checks that username doesn't already exist
3. Generates RSA key pair for the new actor
4. Creates proper ActivityPub actor structure with inbox/outbox/etc
5. Saves the actor to the repository
6. Returns `202 Accepted`

**Auto-Generated Fields:**

The server automatically generates:
- `id` - Full actor URI (e.g., `https://yourdomain.com/users/alice`)
- `inbox`, `outbox`, `followers`, `following` - Standard collection endpoints
- `publicKey` - RSA public key for HTTP signatures
- `privateKeyPem` - RSA private key (stored in extension data)

**Requirements:**

- `preferredUsername` is **required** and becomes the actor's username
- Username must be **unique** and **not already exist**
- Cannot create an actor with the system username

### Update Actor

Updates an existing user's profile.

**Activity Structure:**

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Update",
  "id": "https://yourdomain.com/activities/update-alice-002",
  "actor": "https://yourdomain.com/users/sys",
  "to": ["https://yourdomain.com/users/sys"],
  "object": {
    "type": "Person",
    "preferredUsername": "alice",
    "name": "Alice in Wonderland",
    "summary": "Down the rabbit hole of federation!",
    "icon": {
      "type": "Image",
      "url": "https://example.com/avatars/alice.jpg"
    }
  }
}
```

**What Happens:**

1. Server validates signature and authorization
2. Verifies the actor exists
3. Updates the actor with new properties
4. Preserves the private key if not provided in update
5. Returns `202 Accepted`

**Preserves:**

- Private keys (unless explicitly provided in the update)
- Followers/following relationships

**Restrictions:**

- Cannot update the **system actor**
- Must provide `preferredUsername` to identify which actor to update

### Delete Actor

Removes a user account.

**Activity Structure:**

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Delete",
  "id": "https://yourdomain.com/activities/delete-alice-003",
  "actor": "https://yourdomain.com/users/sys",
  "to": ["https://yourdomain.com/users/sys"],
  "object": {
    "type": "Person",
    "id": "https://yourdomain.com/users/alice"
  }
}
```

Or simply reference by actor ID:

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Delete",
  "id": "https://yourdomain.com/activities/delete-alice-003",
  "actor": "https://yourdomain.com/users/sys",
  "to": ["https://yourdomain.com/users/sys"],
  "object": "https://yourdomain.com/users/alice"
}
```

**What Happens:**

1. Server validates signature and authorization
2. Verifies the actor exists
3. Deletes the actor from the repository
4. Returns `202 Accepted`

**Restrictions:**

- Cannot delete the **system actor**

## Usage Examples

### Using cURL with HTTP Signatures

Here's how to create a user using the system actor's credentials:

```bash
#!/bin/bash

SYSTEM_ACTOR="https://yourdomain.com/users/sys"
INBOX="${SYSTEM_ACTOR}/inbox"
PRIVATE_KEY="/path/to/sys_private.pem"
KEY_ID="${SYSTEM_ACTOR}#main-key"

# Activity to send
ACTIVITY='{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Create",
  "id": "https://yourdomain.com/activities/'$(uuidgen)'",
  "actor": "'${SYSTEM_ACTOR}'",
  "to": ["'${SYSTEM_ACTOR}'"],
  "object": {
    "type": "Person",
    "preferredUsername": "bob",
    "name": "Bob Builder",
    "summary": "Can we fix it? Yes we can!"
  }
}'

# Generate signature and send
# (You'd need an HTTP signature library here)
curl -X POST "${INBOX}" \
  -H "Content-Type: application/activity+json" \
  -H "Date: $(date -u +"%a, %d %b %Y %H:%M:%S GMT")" \
  -H "Signature: keyId=\"${KEY_ID}\",algorithm=\"rsa-sha256\",headers=\"(request-target) host date digest\",signature=\"...\"" \
  --data "${ACTIVITY}"
```

### Using ActivityPub Client Library

If you're using a library that supports HTTP signatures:

```csharp
var client = new ActivityPubClient(new ActivityPubClientOptions 
{
    ActorId = "https://yourdomain.com/users/sys",
    PublicKeyId = "https://yourdomain.com/users/sys#main-key",
    PrivateKeyPem = systemPrivateKey
});

var createActivity = new Activity
{
    Type = new[] { "Create" },
    Id = $"https://yourdomain.com/activities/{Guid.NewGuid()}",
    Actor = new[] { new Link { Href = new Uri("https://yourdomain.com/users/sys") } },
    To = new[] { new Link { Href = new Uri("https://yourdomain.com/users/sys") } },
    Object = new[]
    {
        new Person
        {
            Type = new[] { "Person" },
            PreferredUsername = "carol",
            Name = new[] { "Carol Danvers" },
            Summary = new[] { "Higher, further, faster!" }
        }
    }
};

await client.PostToInboxAsync("https://yourdomain.com/users/sys/inbox", createActivity);
```

## Programmatic Access

Within your application, you can also access the admin handler directly:

```csharp
var adminHandler = serviceProvider.GetRequiredService<AdminOperationsHandler>();

var newActor = new Person
{
    PreferredUsername = "diana",
    Name = new[] { "Diana Prince" },
    Summary = new[] { "Ambassador of truth" }
};

var createActivity = new Activity
{
    Type = new[] { "Create" },
    Actor = new[] { new Link { Href = new Uri(systemActorId) } },
    Object = new[] { newActor }
};

bool success = await adminHandler.HandleAdminActivityAsync(createActivity);
```

## Security Considerations

### Authentication & Authorization

1. **HTTP Signatures Required** - All admin operations must be signed with a valid private key
2. **Explicit Authorization List** - Only actors in `AuthorizedAdminActors` (plus system actor) can perform operations
3. **System Actor Protection** - The system actor itself cannot be modified or deleted
4. **Signature Verification** - Uses the same verification as federated inbox delivery

### Audit Trail

All administrative operations:
- ✅ Are logged with full details (actor, operation, timestamp)
- ✅ Are stored in the activity repository
- ✅ Include the original signed activity for forensic analysis
- ✅ Can be queried from the system actor's inbox

### Rate Limiting

Consider implementing rate limits on the system inbox to prevent abuse:

```csharp
services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("admin-api", config =>
    {
        config.Window = TimeSpan.FromMinutes(1);
        config.PermitLimit = 10;
    });
});
```

## Extensibility

The `AdminOperationsHandler` can be extended to support custom admin operations:

1. Define a custom activity type in your context:

```json
{
  "@context": [
    "https://www.w3.org/ns/activitystreams",
    {
      "broca": "https://yourdomain.com/ns#",
      "GrantRole": "broca:GrantRole"
    }
  ],
  "type": "broca:GrantRole",
  "actor": "https://yourdomain.com/users/sys",
  "object": "https://yourdomain.com/users/alice",
  "target": {
    "type": "broca:Role",
    "name": "moderator"
  }
}
```

2. Extend `HandleUnknownAdminActivityAsync` method to handle your custom types

## Is This Spec-Compliant?

**Yes!** The ActivityPub specification explicitly supports:

1. **Extensibility** - Custom activity types and operations are allowed
2. **Inbox Delivery** - Any activity can be posted to an inbox
3. **Authorization** - Servers are expected to implement their own authorization
4. **Create/Update/Delete** - These are standard activity types

What makes this **spec-compliant**:
- Uses standard ActivityPub delivery mechanism (POST to inbox)
- Uses standard HTTP Signatures for authentication
- Uses standard ActivityStreams vocabulary
- Follows standard activity structure

What makes this **non-standard**:
- Other servers won't understand these specific admin operations
- This is server-specific behavior (which is fine!)

The spec says: *"an Actor may represent a piece of software, like a bot, or an automated process"* (Section 4), and the system actor doing administrative tasks fits this perfectly.

## Comparison to Alternatives

| Approach | Pros | Cons |
|----------|------|------|
| **Admin Back Channel** | • Uses existing auth (HTTP sigs)<br>• Auditable in activity logs<br>• Spec-compliant<br>• Consistent with federation | • Non-standard<br>• Requires HTTP signature tooling |
| **REST API** | • Well-understood<br>• Standard tooling | • Separate auth mechanism<br>• Not part of ActivityPub flow |
| **Direct DB Access** | • Simple for scripts | • No audit trail<br>• Bypasses all validation |

## Troubleshooting

### "Unauthorized administrative operation"

- Check that the actor ID is in `AuthorizedAdminActors`
- Verify the HTTP signature is valid
- Ensure `EnableAdminOperations` is `true`

### "Actor already exists"

- Use `Update` instead of `Create` for existing actors
- Check for username conflicts

### "Cannot update/delete system actor"

- This is intentional protection
- The system actor cannot be modified via admin operations

## References

- [ActivityPub Specification](https://www.w3.org/TR/activitypub/)
- [ActivityStreams Vocabulary](https://www.w3.org/TR/activitystreams-vocabulary/)
- [HTTP Signatures](https://datatracker.ietf.org/doc/html/draft-cavage-http-signatures)
- [Broca System Actor Configuration](./SYSTEM_ACTOR_CONFIGURATION.md)
