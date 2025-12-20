namespace Broca.ActivityPub.Persistence.EntityFramework.Entities;

/// <summary>
/// Entity for storing ActivityPub objects (Notes, Articles, Images, Videos, etc.)
/// </summary>
/// <remarks>
/// This represents the actual content objects that activities operate on.
/// For example, a Create activity might contain a Note object, which would be stored here.
/// </remarks>
public class ObjectEntity
{
    // Primary key - auto-increment integer
    public long Id { get; set; }
    
    // ActivityPub identifiers
    public string ObjectId { get; set; } = string.Empty;     // The AP ID (URI)
    public string ObjectType { get; set; } = string.Empty;   // Note, Article, Image, Video, etc.
    
    // Attribution
    public string AttributedToId { get; set; } = string.Empty;  // Actor who created this
    public string? AttributedToUsername { get; set; }           // Local username if local actor
    
    // Content
    public string? ContentText { get; set; }                 // Plain text content
    public string? ContentHtml { get; set; }                 // HTML content
    public string? Summary { get; set; }                     // Content warning or article summary
    public string? Name { get; set; }                        // Display name/title
    public string? Language { get; set; }                    // ISO 639 language code
    public bool Sensitive { get; set; }                      // Content warning flag
    
    // Threading and conversation
    public string? InReplyTo { get; set; }                   // ID of parent object
    public string? ConversationId { get; set; }              // Thread/conversation identifier
    
    // Timestamps
    public DateTime? Published { get; set; }                 // When object was published
    public DateTime? Updated { get; set; }                   // When object was last updated
    public DateTime CreatedAt { get; set; }                  // When we stored it
    public DateTime? DeletedAt { get; set; }                 // Tombstone timestamp
    
    // Visibility
    public bool IsPublic { get; set; }                       // Quick flag for public objects
    
    // External references
    public string? Url { get; set; }                         // Canonical URL
    public string? RemoteUrl { get; set; }                   // Original URL if federated
    public string? BlobStorageKey { get; set; }              // Reference to raw JSON in blob storage
    
    // Media metadata (for Image, Video, Audio types)
    public string? MediaType { get; set; }                   // MIME type
    public int? Width { get; set; }                          // Image/video width
    public int? Height { get; set; }                         // Image/video height
    public int? Duration { get; set; }                       // Duration in seconds (video/audio)
    
    // Denormalized counts
    public int ReplyCount { get; set; }                      // Number of replies
    public int LikeCount { get; set; }                       // Number of likes
    public int ShareCount { get; set; }                      // Number of shares/announces
    public int AttachmentCount { get; set; }                 // Number of attachments
    
    // Raw JSON for complete data preservation
    public string ObjectJson { get; set; } = string.Empty;
    
    // Navigation properties
    public virtual ICollection<ActivityRecipientEntity> Recipients { get; set; } = new List<ActivityRecipientEntity>();
    public virtual ICollection<ActivityAttachmentEntity> Attachments { get; set; } = new List<ActivityAttachmentEntity>();
    public virtual ICollection<ActivityTagEntity> Tags { get; set; } = new List<ActivityTagEntity>();
}
