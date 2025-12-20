namespace Broca.ActivityPub.Persistence.EntityFramework.Entities;

/// <summary>
/// Entity for storing media attachments (images, videos, audio, documents)
/// </summary>
public class ActivityAttachmentEntity
{
    // Primary key - auto-increment integer
    public long Id { get; set; }
    
    // Foreign keys - one or the other will be populated
    public long? ActivityId { get; set; }                    // FK to ActivityEntity
    public long? ObjectId { get; set; }                      // FK to ObjectEntity
    
    // Attachment information
    public string AttachmentType { get; set; } = string.Empty; // Image, Video, Audio, Document
    public string Url { get; set; } = string.Empty;          // URL to the attachment
    public string? MediaType { get; set; }                   // MIME type (image/jpeg, video/mp4, etc.)
    public string? Name { get; set; }                        // Alt text or filename
    public string? BlurhashValue { get; set; }               // Blurhash for progressive loading
    
    // Media dimensions
    public int? Width { get; set; }                          // Width in pixels (for images/videos)
    public int? Height { get; set; }                         // Height in pixels (for images/videos)
    public int? Duration { get; set; }                       // Duration in seconds (for audio/video)
    public long? SizeBytes { get; set; }                     // File size in bytes
    
    // Ordering
    public int OrderIndex { get; set; }                      // Order in attachment list
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public virtual ActivityEntity? Activity { get; set; }
    public virtual ObjectEntity? Object { get; set; }
}
