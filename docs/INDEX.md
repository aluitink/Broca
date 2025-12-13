# Broca Documentation Index

Complete documentation for the Broca ActivityPub library.

## Getting Started

- **[README.md](./README.md)** - Quick start guide and core concepts
- **[DOCKER.md](./DOCKER.md)** - Docker setup and deployment guide

## Core Features

- **[CLIENT.md](./CLIENT.md)** - ActivityPub client library documentation
  - Anonymous and authenticated clients
  - Activity builder and fluent API
  - HTTP signatures
  - WebFinger resolution

- **[SERVER_IMPLEMENTATION.md](./SERVER_IMPLEMENTATION.md)** - Server implementation guide
  - Actor management
  - Inbox/Outbox handling
  - WebFinger support
  - System actor configuration

- **[ACTIVITY_DELIVERY.md](./ACTIVITY_DELIVERY.md)** - Activity delivery queue system
  - Automatic delivery to followers
  - Background worker architecture
  - Retry logic and error handling
  - Monitoring and production considerations

- **[IDENTITY_PROVIDER.md](./IDENTITY_PROVIDER.md)** - Identity provider system
  - Expose existing users as ActivityPub actors
  - Simple configuration-based setup
  - Custom provider implementation
  - Implementation architecture

## Advanced Topics

- **[BLOB_STORAGE.md](./BLOB_STORAGE.md)** - Blob storage for media attachments
  - File system-based storage implementation
  - Automatic attachment processing
  - URL rewriting for local serving
  - Media controller endpoints

- **[MEDIA_AND_ENDPOINTS.md](./MEDIA_AND_ENDPOINTS.md)** - Media handling and actor endpoints
  - Understanding Mastodon's media API vs ActivityPub federation
  - Actor endpoints property (sharedInbox, uploadMedia)
  - Media upload workflow and patterns
  - Shared inbox implementation

- **[COLLECTIONS.md](./COLLECTIONS.md)** - Collection endpoints reference
  - Followers, Following, Liked collections
  - Pagination with continuation tokens
  - Collection querying

- **[SYSTEM_ACTOR_CONFIGURATION.md](./SYSTEM_ACTOR_CONFIGURATION.md)** - System actor setup
  - Server-level identity configuration
  - Federated server operations

- **[ADMIN_BACK_CHANNEL.md](./ADMIN_BACK_CHANNEL.md)** - Administrative operations via ActivityPub
  - Create, update, and delete users via ActivityPub protocol
  - Spec-compliant administrative back channel
  - HTTP signature-based authorization
  - Security and audit trail

- **[SIGNATURE_VALIDATION_UPDATE.md](./SIGNATURE_VALIDATION_UPDATE.md)** - HTTP signature validation
- **[SIGNATURE_IMPLEMENTATION_SUMMARY.md](./SIGNATURE_IMPLEMENTATION_SUMMARY.md)** - Signature implementation details

## Development & Testing

- **[TESTING.md](./TESTING.md)** - Comprehensive testing guide
  - Test key generation
  - Multi-server federation tests
  - Integration testing strategies

- **[IDENTITY_QUICK_REFERENCE.md](./IDENTITY_QUICK_REFERENCE.md)** - Quick reference for identity management

## Samples & Examples

For working code examples, see:
- **[../samples/README.md](../samples/README.md)** - Sample applications overview
- **[../samples/IDENTITY_EXAMPLES.md](../samples/IDENTITY_EXAMPLES.md)** - Identity provider code examples
- **[../samples/Broca.Sample.WebApi/Examples/](../samples/Broca.Sample.WebApi/Examples/)** - Additional code examples

## API Reference

See the inline XML documentation in the source code for detailed API reference.

## Project Structure

```
Broca/
├── src/
│   ├── Broca.ActivityPub.Core/        # Shared interfaces and models
│   ├── Broca.ActivityPub.Client/      # Client library
│   ├── Broca.ActivityPub.Server/      # Server components
│   ├── Broca.ActivityPub.Persistence/ # Storage abstractions
│   └── Broca.ActivityPub.WebClient/   # Blazor components
├── samples/                            # Working examples
├── tests/                              # Test projects
└── docs/                               # This documentation
```

## External Resources

- [ActivityPub W3C Specification](https://www.w3.org/TR/activitypub/)
- [ActivityStreams 2.0 Vocabulary](https://www.w3.org/TR/activitystreams-vocabulary/)
- [KristofferStrube.ActivityStreams Package](https://github.com/KristofferStrube/ActivityStreams)
