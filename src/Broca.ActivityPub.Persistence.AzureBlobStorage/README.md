# Broca.ActivityPub.Persistence.AzureBlobStorage

Azure Blob Storage implementation for Broca ActivityPub blob storage. Provides scalable, secure cloud storage for media attachments like images, videos, and other binary content.

## Features

- **Scalable Cloud Storage**: Leverages Azure Blob Storage for unlimited scalability
- **CDN Support**: Optional CDN URL configuration for better performance
- **Automatic Container Management**: Creates containers automatically if needed
- **Public/Private Access**: Configurable blob access levels
- **Organized Storage**: Blobs organized by username for better management

## Installation

```bash
dotnet add package Broca.ActivityPub.Persistence.AzureBlobStorage
```

## Usage

### Basic Setup

```csharp
using Broca.ActivityPub.Persistence.AzureBlobStorage.Extensions;

// In your Program.cs or Startup.cs
services.AddAzureBlobStorage(connectionString);
```

### Configuration from appsettings.json

```json
{
  "AzureBlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...;EndpointSuffix=core.windows.net",
    "ContainerName": "activitypub-blobs",
    "BaseUrl": "https://cdn.example.com",
    "CreateContainerIfNotExists": true,
    "PublicAccess": true
  }
}
```

```csharp
services.AddAzureBlobStorage(
    Configuration.GetSection("AzureBlobStorage")
);
```

### Advanced Configuration

```csharp
services.AddAzureBlobStorage(options =>
{
    options.ConnectionString = "your-connection-string";
    options.ContainerName = "my-custom-container";
    options.BaseUrl = "https://cdn.mysite.com"; // Optional CDN URL
    options.CreateContainerIfNotExists = true;
    options.PublicAccess = true; // Allow public read access
});
```

## Connection String

You can find your Azure Storage connection string in the Azure Portal:

1. Go to your Storage Account
2. Navigate to "Access keys" under Settings
3. Copy either key1 or key2 connection string

Connection string format:
```
DefaultEndpointsProtocol=https;AccountName=<account-name>;AccountKey=<account-key>;EndpointSuffix=core.windows.net
```

## CDN Integration

To use Azure CDN or another CDN:

1. Set up a CDN endpoint pointing to your blob storage
2. Configure the `BaseUrl` option with your CDN URL:

```csharp
options.BaseUrl = "https://cdn.example.com";
```

This will generate URLs like:
```
https://cdn.example.com/activitypub-blobs/username/blob-id
```

Instead of:
```
https://myaccount.blob.core.windows.net/activitypub-blobs/username/blob-id
```

## Security Considerations

### Private Storage

For private blob storage (requires authentication to access):

```csharp
services.AddAzureBlobStorage(options =>
{
    options.ConnectionString = connectionString;
    options.PublicAccess = false; // Blobs require authentication
});
```

### Using Managed Identity

For production environments, use Managed Identity instead of connection strings:

```csharp
using Azure.Identity;
using Azure.Storage.Blobs;

services.AddSingleton<IBlobStorageService>(sp =>
{
    var blobServiceClient = new BlobServiceClient(
        new Uri("https://myaccount.blob.core.windows.net"),
        new DefaultAzureCredential()
    );
    
    // Custom implementation using blobServiceClient
    // ... 
});
```

## Local Development

For local development, use the Azurite storage emulator:

```bash
# Install Azurite
npm install -g azurite

# Start Azurite
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

Connection string for Azurite:
```
DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;
```

## Blob Organization

Blobs are organized by username:
```
container-name/
  username1/
    blob-id-1
    blob-id-2
  username2/
    blob-id-3
```

This organization helps with:
- User data management
- Quota enforcement
- Data deletion (when deleting a user)

## Cost Optimization

Tips for optimizing Azure Blob Storage costs:

1. **Use Cool or Archive tiers** for infrequently accessed content
2. **Set up lifecycle management** to automatically move old blobs to cheaper tiers
3. **Enable CDN** to reduce blob storage egress costs
4. **Monitor usage** with Azure Cost Management

## License

See the main Broca repository for license information.
