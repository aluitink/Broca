# Broca.ActivityPub.Persistence.FileSystem

File system-based blob storage for Broca ActivityPub. Provides simple, local storage for media attachments - perfect for development, testing, and single-server deployments.

## Features

- **Simple Setup**: No external dependencies or services required
- **Local Storage**: Stores files on the local file system
- **Organized Structure**: Files organized by username
- **Date-based Organization**: Optional date-based folder structure
- **Metadata Support**: Stores content type and file metadata
- **Production Ready**: Suitable for single-server deployments

## Installation

```bash
dotnet add package Broca.ActivityPub.Persistence.FileSystem
```

## Usage

### Basic Setup

```csharp
using Broca.ActivityPub.Persistence.FileSystem.Extensions;

// In your Program.cs or Startup.cs
services.AddFileSystemBlobStorage(
    dataPath: "data/blobs",
    baseUrl: "https://mysite.com",
    routePrefix: "/media"
);
```

### Configuration from appsettings.json

```json
{
  "FileSystemBlobStorage": {
    "DataPath": "data/blobs",
    "BaseUrl": "https://mysite.com",
    "RoutePrefix": "/media",
    "MaxFileSizeBytes": 52428800,
    "OrganizeByDate": false
  }
}
```

```csharp
services.AddFileSystemBlobStorage(
    Configuration.GetSection("FileSystemBlobStorage")
);
```

### Advanced Configuration

```csharp
services.AddFileSystemBlobStorage(options =>
{
    options.DataPath = "/var/activitypub/blobs";
    options.BaseUrl = "https://mysite.com";
    options.RoutePrefix = "/media";
    options.MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB
    options.OrganizeByDate = true; // Organize by date: username/YYYY/MM/DD/blob-id
});
```

## File Organization

### Default Organization

```
data/blobs/
  username1/
    blob-id-1.jpg
    blob-id-1.jpg.meta
    blob-id-2.png
    blob-id-2.png.meta
  username2/
    blob-id-3.mp4
```

### Date-based Organization

When `OrganizeByDate` is enabled:

```
data/blobs/
  username1/
    2025/
      12/
        20/
          blob-id-1.jpg
          blob-id-1.jpg.meta
  username2/
    2025/
      12/
        21/
          blob-id-3.mp4
```

## Serving Files

You need to configure your web server to serve the blob files. Here are examples for common setups:

### ASP.NET Core Static Files

```csharp
// In Program.cs
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "data", "blobs")),
    RequestPath = "/media"
});
```

### Nginx

```nginx
location /media/ {
    alias /var/activitypub/blobs/;
    
    # Optional: Set cache headers
    expires 1y;
    add_header Cache-Control "public, immutable";
}
```

### Apache

```apache
Alias /media /var/activitypub/blobs

<Directory /var/activitypub/blobs>
    Options -Indexes +FollowSymLinks
    Require all granted
    
    # Optional: Set cache headers
    ExpiresActive On
    ExpiresDefault "access plus 1 year"
</Directory>
```

## Docker Volumes

When using Docker, mount the blob storage directory as a volume:

```yaml
services:
  activitypub-server:
    image: myactivitypub:latest
    volumes:
      - ./data/blobs:/app/data/blobs
    environment:
      - FileSystemBlobStorage__DataPath=/app/data/blobs
      - FileSystemBlobStorage__BaseUrl=https://mysite.com
```

## Backup Considerations

The file system blob storage is easy to backup:

```bash
# Simple backup
tar -czf blobs-backup-$(date +%Y%m%d).tar.gz data/blobs/

# Incremental backup with rsync
rsync -av --delete data/blobs/ /backup/location/blobs/
```

## Production Recommendations

### For Single Server Deployments

File system storage is perfectly suitable for production on a single server:

1. **Use a dedicated disk/partition** for blob storage
2. **Set up regular backups** of the blob directory
3. **Monitor disk space** to avoid running out of storage
4. **Use a CDN or reverse proxy cache** to reduce server load

### For Multi-Server Deployments

For multiple servers, consider:

1. **Network File System (NFS)**: Mount a shared NFS volume
2. **Azure Blob Storage**: Use `Broca.ActivityPub.Persistence.AzureBlobStorage`
3. **Object Storage**: Consider S3-compatible storage solutions

## Performance Tips

1. **SSD Storage**: Use SSD for better I/O performance
2. **Separate Volume**: Keep blob storage on a separate volume from the application
3. **Cache Headers**: Configure proper cache headers in your web server
4. **CDN**: Use a CDN for better global performance

## Supported File Types

The service includes built-in MIME type detection for:

- **Images**: JPG, PNG, GIF, WebP, SVG, BMP, ICO
- **Videos**: MP4, WebM, OGV, AVI, MOV
- **Audio**: MP3, OGG, WAV, FLAC, M4A
- **Documents**: PDF, TXT, HTML, JSON

## Security Considerations

1. **Path Sanitization**: Automatically sanitizes usernames and blob IDs to prevent path traversal
2. **File Size Limits**: Configure `MaxFileSizeBytes` to prevent abuse
3. **Access Control**: Implement authentication/authorization at the web server level if needed
4. **Virus Scanning**: Consider integrating virus scanning for uploaded content

## Migration

### Moving to Cloud Storage

To migrate from file system to cloud storage:

```bash
# Upload all blobs to Azure Blob Storage
az storage blob upload-batch \
  --destination activitypub-blobs \
  --source data/blobs \
  --account-name myaccount
```

Then switch your configuration to use `Broca.ActivityPub.Persistence.AzureBlobStorage`.

## License

See the main Broca repository for license information.
