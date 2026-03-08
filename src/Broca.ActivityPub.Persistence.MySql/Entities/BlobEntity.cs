namespace Broca.ActivityPub.Persistence.MySql.Entities;

public class BlobEntity
{
    public string Username { get; set; } = string.Empty;
    public string BlobId { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string StorageProvider { get; set; } = "mysql";
    public string? StorageKey { get; set; }
    public byte[]? Content { get; set; }
    public long? Size { get; set; }
}
