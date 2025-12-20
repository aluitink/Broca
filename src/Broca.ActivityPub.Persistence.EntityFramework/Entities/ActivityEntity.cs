namespace Broca.ActivityPub.Persistence.EntityFramework.Entities;

/// <summary>
/// Entity for storing activity data with extracted ActivityPub/ActivityStreams fields
/// </summary>
public class ActivityEntity
{
    // Primary key - auto-increment integer
    public long Id { get; set; }
    
    // ActivityPub identifiers
    public string ActivityId { get; set; } = string.Empty;  // The AP ID (URI)
    public string Username { get; set; } = string.Empty;     // Local username this activity belongs to
    
    // Activity type and core fields
    public string ActivityType { get; set; } = string.Empty; // Create, Update, Delete, Follow, Like, Announce, etc.
    public string ActorId { get; set; } = string.Empty;      // Who performed the activity
    public string? ObjectId { get; set; }                    // What was acted upon
    public string? ObjectType { get; set; }                  // Type of object (Note, Article, Person, etc.)
    public string? TargetId { get; set; }                    // Target (for Add/Remove operations)
    public string? TargetType { get; set; }                  // Type of target
    
    // Threading and conversation
    public string? InReplyTo { get; set; }                   // ID of parent activity/object
    public string? ConversationId { get; set; }              // Thread/conversation identifier
    
    // Timestamps
    public DateTime? Published { get; set; }                 // When activity was published
    public DateTime? Updated { get; set; }                   // When activity was last updated
    public DateTime CreatedAt { get; set; }                  // When we stored it
    
    // Content fields (for Create activities with Notes/Articles)
    public string? ContentText { get; set; }                 // Plain text content
    public string? ContentHtml { get; set; }                 // HTML content
    public string? Summary { get; set; }                     // Content warning or summary
    public string? Language { get; set; }                    // ISO 639 language code
    public bool Sensitive { get; set; }                      // Content warning flag
    
    // Visibility and routing
    public bool IsPublic { get; set; }                       // Quick flag for public activities
    public bool IsInbox { get; set; }                        // Is this in the inbox?
    public bool IsOutbox { get; set; }                       // Is this in the outbox?
    
    // External references
    public string? RemoteUrl { get; set; }                   // Original URL if federated
    public string? BlobStorageKey { get; set; }              // Reference to raw JSON in blob storage
    
    // Denormalized counts for performance
    public int ReplyCount { get; set; }                      // Number of replies
    public int LikeCount { get; set; }                       // Number of likes
    public int ShareCount { get; set; }                      // Number of shares/announces
    public int AttachmentCount { get; set; }                 // Number of attachments
    
    // Raw JSON for complete data preservation
    public string ActivityJson { get; set; } = string.Empty;
    
    // Navigation properties
    public virtual ICollection<ActivityRecipientEntity> Recipients { get; set; } = new List<ActivityRecipientEntity>();
    public virtual ICollection<ActivityAttachmentEntity> Attachments { get; set; } = new List<ActivityAttachmentEntity>();
    public virtual ICollection<ActivityTagEntity> Tags { get; set; } = new List<ActivityTagEntity>();
}
