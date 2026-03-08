namespace Broca.ActivityPub.Persistence.MySql.Entities;

public class BlobEntity
{
    public string Username { get; set; } = string.Empty;
    public string BlobId { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
}
