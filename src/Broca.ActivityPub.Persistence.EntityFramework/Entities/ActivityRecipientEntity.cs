namespace Broca.ActivityPub.Persistence.EntityFramework.Entities;

/// <summary>
/// Entity for storing activity/object recipients (To, Cc, Bcc addressing)
/// </summary>
public class ActivityRecipientEntity
{
    // Primary key - auto-increment integer
    public long Id { get; set; }
    
    // Foreign keys - one or the other will be populated
    public long? ActivityId { get; set; }                    // FK to ActivityEntity
    public long? ObjectId { get; set; }                      // FK to ObjectEntity
    
    // Recipient information
    public string RecipientType { get; set; } = string.Empty; // "To", "Cc", "Bcc"
    public string RecipientAddress { get; set; } = string.Empty; // Actor ID or collection URL
    
    // Quick lookup flags
    public bool IsPublic { get; set; }                       // Is this the Public collection?
    public bool IsFollowers { get; set; }                    // Is this a followers collection?
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public virtual ActivityEntity? Activity { get; set; }
    public virtual ObjectEntity? Object { get; set; }
}
