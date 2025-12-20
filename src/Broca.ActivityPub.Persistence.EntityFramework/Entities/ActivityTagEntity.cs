namespace Broca.ActivityPub.Persistence.EntityFramework.Entities;

/// <summary>
/// Entity for storing tags (hashtags, mentions, other semantic tags)
/// </summary>
public class ActivityTagEntity
{
    // Primary key - auto-increment integer
    public long Id { get; set; }
    
    // Foreign keys - one or the other will be populated
    public long? ActivityId { get; set; }                    // FK to ActivityEntity
    public long? ObjectId { get; set; }                      // FK to ObjectEntity
    
    // Tag information
    public string TagType { get; set; } = string.Empty;      // "Hashtag", "Mention", "Emoji", etc.
    public string Name { get; set; } = string.Empty;         // Tag name (e.g., "#activitypub", "@user@domain")
    public string? Href { get; set; }                        // URL for the tag (actor URL for mentions)
    
    // For emoji tags
    public string? IconUrl { get; set; }                     // Custom emoji image URL
    public string? IconMediaType { get; set; }               // MIME type of emoji image
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public virtual ActivityEntity? Activity { get; set; }
    public virtual ObjectEntity? Object { get; set; }
}
