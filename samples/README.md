# Broca Sample Applications

This directory contains sample applications demonstrating the use of Broca ActivityPub libraries.

## Broca.Sample.WebApi

A minimal ASP.NET Core Web API server implementation of ActivityPub using Broca.ActivityPub.Server.

**Features:**
- Full ActivityPub server implementation
- WebFinger support for user discovery
- ActivityPub inbox/outbox endpoints
- File-based persistence
- Custom identity provider example (see `Examples/CustomProviderExample.cs`)

**Configuration:**
- `ActivityPub__BaseUrl`: Public URL of the server (e.g., https://dev.broca.luit.ink)
- `ActivityPub__PrimaryDomain`: Primary domain for the server
- `ActivityPub__ServerName`: Display name for the server
- `ActivityPub__SystemActorUsername`: Username for the system actor
- `ActivityPub__RoutePrefix`: URL prefix for ActivityPub endpoints (default: "ap")

See also:
- [IDENTITY_EXAMPLES.md](./IDENTITY_EXAMPLES.md) - Identity provider setup examples
- [ADMIN_EXAMPLES.md](./ADMIN_EXAMPLES.md) - Examples of administrative operations via ActivityPub
- [BLOB_STORAGE_EXAMPLES.md](./BLOB_STORAGE_EXAMPLES.md) - Blob storage and media attachment examples

## Broca.Sample.BlazorApp

A client-side Blazor WebAssembly application for browsing ActivityPub content using Broca.ActivityPub.Client.

**Features:**
- 🔍 **User Lookup**: Search for ActivityPub users by their alias (user@domain.com)
- 📬 **Outbox Browser**: View a user's public activities
- 🔐 **Identity Support**: Load your public/private key pair from a JSON file
- 💻 **Client-Side Only**: Runs entirely in the browser using Blazor WASM

**Architecture:**
- Built as a Blazor WebAssembly application
- Uses nginx to serve static files in Docker
- Makes ActivityPub requests directly from the browser
- No backend server required (fully static)

### Using the BlazorApp

1. **Browse Anonymously**: Navigate to `/browse` and enter a user alias like `user@mastodon.social`
2. **Load Identity**: Navigate to `/identity` and upload a JSON file with your keys (see format below)
3. **View Outbox**: After looking up a user, click "Load Outbox" to see their activities

### Identity JSON Format

To authenticate requests, create a JSON file with the following structure:

```json
{
  "actorId": "https://example.com/users/alice",
  "username": "alice",
  "domain": "example.com",
  "publicKey": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----",
  "privateKey": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----"
}
```

**Important:** Your identity is only stored in browser memory and is cleared when you close the tab.

## Docker Compose Setup

Both sample applications are included in the Docker Compose configuration.

### Local Development

```bash
# Start both services locally
docker-compose up -d

# WebAPI will be available at: http://localhost:5050
# BlazorApp will be available at: http://localhost:5051
```

### Production Deployment

The `docker-compose.override.yml` configures the services for production with:
- WebAPI: https://dev.broca.luit.ink
- BlazorApp: https://blazor.dev.broca.luit.ink
- Automatic HTTPS via Let's Encrypt
- nginx-proxy reverse proxy integration

```bash
# Deploy to production
docker-compose -f docker-compose.yml -f docker-compose.override.yml up -d
```

## Building Locally

### WebAPI
```bash
cd samples/Broca.Sample.WebApi
dotnet build
dotnet run
```

### BlazorApp
```bash
cd samples/Broca.Sample.BlazorApp
dotnet build
dotnet run
```

## Testing

Try looking up these public ActivityPub users:
- `Gargron@mastodon.social` - Mastodon creator
- Any user from public Mastodon instances

## Security Notes

- The BlazorApp identity loading feature is for demonstration purposes
- In production, consider more secure key management solutions
- Private keys loaded in the browser are only stored in memory
- Always use HTTPS in production for ActivityPub communication
