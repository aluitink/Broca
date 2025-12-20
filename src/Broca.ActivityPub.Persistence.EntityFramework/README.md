# Broca.ActivityPub.Persistence.EntityFramework

Entity Framework Core persistence layer for Broca ActivityPub. This package provides database-agnostic storage for actors, activities, and delivery queues.

## Features

- **Database Agnostic**: Works with any Entity Framework Core provider (SQL Server, PostgreSQL, SQLite, MySQL, etc.)
- **Repository Pattern**: Implements `IActorRepository`, `IActivityRepository`, and `IDeliveryQueueRepository`
- **Strongly Typed Entities**: Optimized database schema with indexes
- **Migration Support**: Built-in support for EF Core migrations

## Installation

```bash
dotnet add package Broca.ActivityPub.Persistence.EntityFramework
```

You'll also need to install your preferred database provider:

```bash
# SQL Server
dotnet add package Microsoft.EntityFrameworkCore.SqlServer

# PostgreSQL
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL

# SQLite
dotnet add package Microsoft.EntityFrameworkCore.Sqlite

# MySQL
dotnet add package Pomelo.EntityFrameworkCore.MySql
```

## Usage

### Basic Setup

```csharp
using Broca.ActivityPub.Persistence.EntityFramework.Extensions;

// In your Program.cs or Startup.cs
services.AddActivityPubEntityFramework(options =>
{
    // Choose your database provider
    options.UseSqlServer(connectionString);
    // or
    options.UseNpgsql(connectionString);
    // or
    options.UseSqlite(connectionString);
});
```

### Apply Migrations

```csharp
// At application startup
await app.Services.MigrateActivityPubDatabaseAsync();
```

### Create Migrations

```bash
# From the project directory
dotnet ef migrations add InitialCreate --context ActivityPubDbContext

# Apply migrations
dotnet ef database update --context ActivityPubDbContext
```

## Database Schema

The package creates the following tables:

- **Actors**: Stores actor profiles
- **Activities**: Stores inbox/outbox activities
- **Followers**: Stores follower relationships
- **Following**: Stores following relationships
- **DeliveryQueue**: Stores pending activity deliveries

## Configuration Examples

### SQL Server with connection string

```csharp
services.AddActivityPubEntityFramework(options =>
    options.UseSqlServer(
        Configuration.GetConnectionString("ActivityPub"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()
    )
);
```

### PostgreSQL with connection string

```csharp
services.AddActivityPubEntityFramework(options =>
    options.UseNpgsql(
        Configuration.GetConnectionString("ActivityPub"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()
    )
);
```

### SQLite (great for development/testing)

```csharp
services.AddActivityPubEntityFramework(options =>
    options.UseSqlite("Data Source=activitypub.db")
);
```

## Advanced Usage

### Custom Configuration

```csharp
services.AddActivityPubEntityFramework(options =>
{
    options.UseSqlServer(connectionString);
    
    // Enable sensitive data logging in development
    options.EnableSensitiveDataLogging();
    
    // Enable detailed errors
    options.EnableDetailedErrors();
});
```

### Using with Docker

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: activitypub
      POSTGRES_USER: activitypub
      POSTGRES_PASSWORD: your_password
    ports:
      - "5432:5432"
```

Connection string: `Host=localhost;Database=activitypub;Username=activitypub;Password=your_password`

## License

See the main Broca repository for license information.
