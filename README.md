# Broca

A modular .NET library for building ActivityPub-enabled applications.

## Overview

Broca provides a complete, standards-compliant implementation of the ActivityPub protocol for .NET developers. It offers client, server, and web client capabilities with a clean, fluent API that makes federation easy.

## Features

- **ActivityPub Client** - Connect to the fediverse with anonymous or authenticated clients
- **ActivityPub Server** - Host your own actors, handle inbox/outbox activities
- **Activity Delivery Queue** - Automatic background delivery to followers with retry logic
- **Identity Provider System** - Easily expose existing users/content as ActivityPub actors
- **HTTP Signatures** - Full support for authenticated requests (required by Mastodon and others)
- **WebFinger** - Discover actors via @user@domain.tld aliases
- **Fluent Activity Builder** - Create activities with a clean, type-safe API
- **Modular Persistence** - Use in-memory, Entity Framework Core, or bring your own storage
- **Standards Compliant** - Uses KristofferStrube.ActivityStreams for spec-compliant types

## Quick Start

### Install

```bash
dotnet add package Broca.ActivityPub.Client
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

// Add Broca server
builder.Services.AddBrocaServer(builder.Configuration);

// Add simple identity provider (configured via appsettings.json)
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

Now your blog is followable as `@alice@myblog.com` from Mastodon!

## Project Structure

- **Broca.ActivityPub.Core** - Shared interfaces and models
- **Broca.ActivityPub.Client** - Client library for federated requests
- **Broca.ActivityPub.Server** - Server components for hosting actors
- **Broca.ActivityPub.Persistence** - Storage abstractions and implementations
- **Broca.ActivityPub.WebClient** - Blazor components (under development)

## Documentation

See the [docs](./docs) folder for detailed documentation:

- **[README.md](./docs/README.md)** - Comprehensive getting started guide
- **[CLIENT.md](./docs/CLIENT.md)** - Client library documentation
- **[IDENTITY_PROVIDER.md](./docs/IDENTITY_PROVIDER.md)** - Identity provider system guide
- **[COLLECTIONS.md](./docs/COLLECTIONS.md)** - Collection endpoints reference
- **[SERVER_IMPLEMENTATION.md](./docs/SERVER_IMPLEMENTATION.md)** - Server implementation guide
- **[DOCKER.md](./docs/DOCKER.md)** - Docker setup and deployment guide
- **[TESTING.md](./docs/TESTING.md)** - Testing guide with key generation and multi-server tests

## Examples

Check out the [samples](./samples) folder for complete working examples:

- **Broca.Sample.MinimalApi** - Minimal API setup
- **Broca.Sample.WebApi** - Full Web API with controllers
- **Broca.Sample.BlazorApp** - Blazor with ActivityPub integration

## Production Deployment with HTTPS

ActivityPub **requires HTTPS** in production. The easiest way to set this up is using nginx-proxy with acme-companion for automatic Let's Encrypt certificates.

### Setup nginx-proxy with Let's Encrypt

First, create the nginx-proxy network and start the proxy containers:

```bash
# Create the network
docker network create nginx-proxy

# Start nginx-proxy with Let's Encrypt companion
docker run -d -p 80:80 -p 443:443 \
  --name nginx-proxy \
  --network nginx-proxy \
  -v /var/run/docker.sock:/tmp/docker.sock:ro \
  -v nginx-certs:/etc/nginx/certs \
  -v nginx-vhost:/etc/nginx/vhost.d \
  -v nginx-html:/usr/share/nginx/html \
  nginxproxy/nginx-proxy

docker run -d \
  --name nginx-proxy-acme \
  --network nginx-proxy \
  --volumes-from nginx-proxy \
  -v /var/run/docker.sock:/var/run/docker.sock:ro \
  -v acme-state:/etc/acme.sh \
  nginxproxy/acme-companion
```

### Configure Your Broca Service

Create a `docker-compose.override.yml` to add HTTPS support:

```yaml
services:
  broca-blazor:
    environment:
      - VIRTUAL_HOST=yourdomain.com
      - VIRTUAL_PORT=80
      - LETSENCRYPT_HOST=yourdomain.com
      - LETSENCRYPT_EMAIL=your-email@example.com
    networks:
      - broca-network
      - nginx-proxy

  broca-api:
    environment:
      - ActivityPub__BaseUrl=https://yourdomain.com
      - ActivityPub__PrimaryDomain=yourdomain.com

networks:
  nginx-proxy:
    external: true
```

Then start your services:

```bash
cd samples
docker-compose up -d
```

The acme-companion will automatically:
- Request Let's Encrypt certificates for your domain
- Renew certificates before they expire
- Configure nginx to serve your application over HTTPS

Your ActivityPub server will be accessible at `https://yourdomain.com` and actors can be followed as `@username@yourdomain.com`.

**Note**: Make sure your domain's DNS A record points to your server's IP address before starting the containers.

## Requirements

- .NET 9.0 or later
- ASP.NET Core for server components
- Docker (optional, for containerized deployment)
- Valid domain name with HTTPS for production ActivityPub federation

## License

[Your license here]

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](./CONTRIBUTING.md) for development workflow and guidelines.
