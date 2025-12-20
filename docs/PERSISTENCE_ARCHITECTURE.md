# Broca ActivityPub Persistence Architecture

This document outlines the persistence layer architecture for Broca ActivityPub, including the available implementations and how to use them.

## Overview

Broca ActivityPub provides a modular persistence architecture with two main categories:

1. **Actor/Activity Storage** - For storing actors (users), activities (posts, likes, follows, etc.), and delivery queues
2. **Blob Storage** - For storing binary media attachments (images, videos, audio, documents)

Each category has multiple implementations packaged as separate NuGet packages, allowing implementors to mix and match based on their needs.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                  Broca.ActivityPub.Core                      │
│                                                               │
│  Interfaces:                                                  │
│    - IActorRepository                                         │
│    - IActivityRepository                                      │
│    - IDeliveryQueueRepository                                 │
│    - IBlobStorageService                                      │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ Implements
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              Actor/Activity Implementations                   │
│                                                               │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Broca.ActivityPub.Persistence.EntityFramework         │  │
│  │  - Works with any EF Core provider                     │  │
│  │  - SQL Server, PostgreSQL, SQLite, MySQL, etc.         │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                               │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Broca.ActivityPub.Persistence (InMemory)              │  │
│  │  - For testing/development                              │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                  Blob Storage Implementations                 │
│                                                               │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Broca.ActivityPub.Persistence.FileSystem              │  │
│  │  - Local file system storage                            │  │
│  │  - Perfect for single-server deployments                │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                               │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Broca.ActivityPub.Persistence.AzureBlobStorage        │  │
│  │  - Cloud-based blob storage                             │  │
│  │  - Scalable, CDN-friendly                               │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## Available Packages

### Actor/Activity Storage

#### Broca.ActivityPub.Persistence.EntityFramework
- **Purpose**: Database storage for actors, activities, and delivery queues
- **Database Agnostic**: Works with any Entity Framework Core provider
- **Supported Databases**: SQL Server, PostgreSQL, SQLite, MySQL, MariaDB, Oracle, etc.
- **Features**:
  - Strongly-typed entities with optimized indexes
  - Built-in migration support
  - Relationship tracking (followers, following)
  - Delivery queue with retry logic

**Installation**:
```bash
dotnet add package Broca.ActivityPub.Persistence.EntityFramework
# Plus your chosen database provider:
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL  # PostgreSQL
# or
dotnet add package Microsoft.EntityFrameworkCore.SqlServer  # SQL Server
# or
dotnet add package Microsoft.EntityFrameworkCore.Sqlite  # SQLite
```

**Usage**:
```csharp
// With PostgreSQL
services.AddActivityPubEntityFramework(options =>
    options.UseNpgsql(connectionString)
);

// Apply migrations
await app.Services.MigrateActivityPubDatabaseAsync();
```

#### Broca.ActivityPub.Persistence (InMemory)
- **Purpose**: In-memory storage for testing and development
- **Not for Production**: Data lost on restart
- **Included in**: Base persistence package

### Blob Storage

#### Broca.ActivityPub.Persistence.FileSystem
- **Purpose**: Local file system storage for media attachments
- **Best For**: 
  - Single-server deployments
  - Development and testing
  - Self-hosted instances
- **Features**:
  - Simple setup with no external dependencies
  - Organized by username
  - Optional date-based organization
  - Metadata support (content type, size, timestamps)

**Installation**:
```bash
dotnet add package Broca.ActivityPub.Persistence.FileSystem
```

**Usage**:
```csharp
services.AddFileSystemBlobStorage(
    dataPath: "data/blobs",
    baseUrl: "https://mysite.com",
    routePrefix: "/media"
);
```

#### Broca.ActivityPub.Persistence.AzureBlobStorage
- **Purpose**: Cloud-based blob storage using Azure Blob Storage
- **Best For**:
  - Multi-server deployments
  - High-traffic sites
  - Global content delivery
  - Scalable storage needs
- **Features**:
  - Unlimited scalability
  - CDN integration
  - Public/private access control
  - Organized by username

**Installation**:
```bash
dotnet add package Broca.ActivityPub.Persistence.AzureBlobStorage
```

**Usage**:
```csharp
services.AddAzureBlobStorage(options =>
{
    options.ConnectionString = azureConnectionString;
    options.ContainerName = "activitypub-blobs";
    options.BaseUrl = "https://cdn.mysite.com"; // Optional CDN
});
```

## Common Usage Patterns

### Pattern 1: Development/Testing Setup
Use in-memory storage for quick iteration:

```csharp
// In appsettings.Development.json
{
  "Persistence": {
    "Type": "InMemory"
  }
}

// In Program.cs
if (builder.Environment.IsDevelopment())
{
    services.AddInMemoryPersistence();
}
```

### Pattern 2: Single Server Self-Hosted
Use SQLite + File System:

```csharp
services.AddActivityPubEntityFramework(options =>
    options.UseSqlite("Data Source=activitypub.db")
);

services.AddFileSystemBlobStorage(
    dataPath: "/var/activitypub/blobs",
    baseUrl: "https://myserver.com",
    routePrefix: "/media"
);
```

### Pattern 3: Production with PostgreSQL + Local Storage
Use PostgreSQL + File System with NFS:

```csharp
services.AddActivityPubEntityFramework(options =>
    options.UseNpgsql(Configuration.GetConnectionString("ActivityPub"))
);

services.AddFileSystemBlobStorage(
    dataPath: "/mnt/nfs/activitypub/blobs",
    baseUrl: "https://mysite.com",
    routePrefix: "/media"
);
```

### Pattern 4: Cloud-Native Multi-Server
Use PostgreSQL + Azure Blob Storage:

```csharp
services.AddActivityPubEntityFramework(options =>
    options.UseNpgsql(Configuration.GetConnectionString("ActivityPub"))
);

services.AddAzureBlobStorage(
    Configuration.GetConnectionString("AzureStorage"),
    containerName: "activitypub-media"
);
```

### Pattern 5: SQL Server + Azure
Use SQL Server + Azure Blob Storage:

```csharp
services.AddActivityPubEntityFramework(options =>
    options.UseSqlServer(Configuration.GetConnectionString("ActivityPub"))
);

services.AddAzureBlobStorage(options =>
{
    options.ConnectionString = Configuration.GetConnectionString("AzureStorage");
    options.BaseUrl = "https://cdn.mysite.com";
});
```

## Database Schema (Entity Framework)

The Entity Framework implementation creates the following tables:

### Actors
- `Id` (PK, bigint)
- `Username` (unique index)
- `ActorId` (unique index, URI)
- `ActorJson` (full actor object)
- `CreatedAt`, `UpdatedAt`

### Activities
- `Id` (PK, bigint)
- `ActivityId` (unique index, URI)
- `Username` (index)
- `ActivityType` (index)
- `ActivityJson` (full activity object)
- `IsInbox`, `IsOutbox` (boolean flags)
- `CreatedAt`

Composite index on: `(Username, ActivityType, CreatedAt)`

### Followers
- `Id` (PK, bigint)
- `Username` (index)
- `FollowerActorId` (URI of follower)
- `CreatedAt`

Unique index on: `(Username, FollowerActorId)`

### Following
- `Id` (PK, bigint)
- `Username` (index)
- `FollowingActorId` (URI being followed)
- `CreatedAt`

Unique index on: `(Username, FollowingActorId)`

### DeliveryQueue
- `Id` (PK, bigint)
- `TargetInbox` (URI)
- `ActivityJson` (activity to deliver)
- `Attempts` (retry count)
- `CreatedAt`, `NextAttempt`
- `Status` (Pending, Delivered, Failed)
- `LastError` (nullable)

Index on: `(Status, NextAttempt)`

## Migration Guide

### Creating Migrations

```bash
# Navigate to the EF project directory
cd src/Broca.ActivityPub.Persistence.EntityFramework

# Add a migration
dotnet ef migrations add InitialCreate

# Update database
dotnet ef database update
```

### Programmatic Migration

```csharp
// At application startup
await app.Services.MigrateActivityPubDatabaseAsync();
```

## Performance Considerations

### Actor/Activity Storage
1. **Indexes**: All implementations use appropriate indexes for common queries
2. **Pagination**: Use limit/offset parameters for large result sets
3. **Caching**: Consider adding a caching layer (Redis) for frequently accessed actors
4. **Connection Pooling**: Configure appropriate connection pool sizes for your database

### Blob Storage
1. **CDN**: Use a CDN for blob content delivery (especially for cloud storage)
2. **Compression**: Consider compressing images/videos before storage
3. **Cache Headers**: Set appropriate cache headers for static content
4. **Chunked Upload**: Implement chunked uploads for large files

## Security Considerations

### Database
1. Use connection strings with least-privilege credentials
2. Enable SSL/TLS for database connections
3. Regularly backup your database
4. Use parameterized queries (handled by EF Core)

### Blob Storage
1. **File System**: 
   - Sanitize paths (automatic in our implementation)
   - Set appropriate file permissions
   - Monitor disk usage
2. **Azure**: 
   - Use Managed Identity in production
   - Configure appropriate access policies
   - Enable soft delete for accidental deletion protection

## Testing

Each persistence implementation should be tested with:

```csharp
// Integration tests
public class PersistenceTests
{
    [Fact]
    public async Task CanStoreAndRetrieveActor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddActivityPubEntityFramework(options =>
            options.UseSqlite("Data Source=:memory:")
        );
        
        var provider = services.BuildServiceProvider();
        await provider.EnsureActivityPubDatabaseCreatedAsync();
        
        var actorRepo = provider.GetRequiredService<IActorRepository>();
        
        // Act & Assert
        // ... test implementation
    }
}
```

## Future Implementations

Potential future persistence implementations:

### Actor/Activity Storage
- MongoDB (document database)
- Cassandra (wide-column store)
- Redis (with persistence)
- DynamoDB

### Blob Storage
- Amazon S3
- Google Cloud Storage
- MinIO (S3-compatible)
- Cloudflare R2

## Contributing

When adding a new persistence implementation:

1. Create a new project: `Broca.ActivityPub.Persistence.[ImplementationName]`
2. Implement the appropriate interfaces from `Broca.ActivityPub.Core.Interfaces`
3. Add extension methods for DI registration
4. Write comprehensive tests
5. Add README.md with usage examples
6. Update this document

## Support

For issues, questions, or contributions related to persistence layers, please see the main Broca repository.
