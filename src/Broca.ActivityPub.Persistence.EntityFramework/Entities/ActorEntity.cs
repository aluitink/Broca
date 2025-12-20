namespace Broca.ActivityPub.Persistence.EntityFramework.Entities;

/// <summary>
/// Entity for storing actor data with detailed ActivityPub fields
/// </summary>
public class ActorEntity
{
    // Primary key - auto-increment integer
    public long Id { get; set; }
    
    // ActivityPub identifiers
    public string Username { get; set; } = string.Empty;     // Local username (unique)
    public string ActorId { get; set; } = string.Empty;      // The AP ID (URI)
    public string ActorType { get; set; } = "Person";        // Person, Service, Group, Organization, Application
    
    // Profile information
    public string? PreferredUsername { get; set; }           // Display username (may differ from Username)
    public string? DisplayName { get; set; }                 // Display name
    public string? Summary { get; set; }                     // Bio/description (HTML allowed)
    public string? SummaryText { get; set; }                 // Plain text bio
    
    // Profile media
    public string? IconUrl { get; set; }                     // Avatar/profile picture URL
    public string? ImageUrl { get; set; }                    // Header/banner image URL
    
    // Cryptographic keys
    public string? PublicKeyPem { get; set; }                // RSA public key in PEM format
    public string? PrivateKeyPem { get; set; }               // RSA private key in PEM format (for local actors)
    public string? PublicKeyId { get; set; }                 // Public key ID (URI)
    
    // ActivityPub endpoints
    public string? InboxUrl { get; set; }                    // Inbox URL
    public string? OutboxUrl { get; set; }                   // Outbox URL
    public string? FollowersUrl { get; set; }                // Followers collection URL
    public string? FollowingUrl { get; set; }                // Following collection URL
    public string? LikedUrl { get; set; }                    // Liked collection URL
    public string? FeaturedUrl { get; set; }                 // Featured/pinned posts URL
    public string? SharedInboxUrl { get; set; }              // Shared inbox URL
    
    // Profile URLs and links
    public string? Url { get; set; }                         // Profile webpage URL
    public string? RemoteUrl { get; set; }                   // Original URL if remote actor
    
    // Privacy and discovery settings
    public bool ManuallyApprovesFollowers { get; set; }      // Locked account
    public bool Discoverable { get; set; } = true;           // Show in directory
    public bool Indexable { get; set; } = true;              // Allow search engine indexing
    public bool Bot { get; set; }                            // Is this a bot account?
    
    // Additional metadata
    public string? Language { get; set; }                    // Preferred language (ISO 639)
    public DateTime? MovedTo { get; set; }                   // Timestamp if account was moved
    public string? MovedToActorId { get; set; }              // New actor ID if moved
    public bool Suspended { get; set; }                      // Is account suspended?
    public DateTime? SuspendedAt { get; set; }               // When account was suspended
    
    // Denormalized counts
    public int FollowersCount { get; set; }                  // Number of followers
    public int FollowingCount { get; set; }                  // Number of following
    public int StatusesCount { get; set; }                   // Number of posts
    
    // Timestamps
    public DateTime CreatedAt { get; set; }                  // When we stored this actor
    public DateTime UpdatedAt { get; set; }                  // Last update
    public DateTime? LastFetchedAt { get; set; }             // Last time we fetched from remote
    
    // Raw JSON for complete data preservation
    public string ActorJson { get; set; } = string.Empty;
    
    // References
    public string? BlobStorageKey { get; set; }              // Reference to raw JSON in blob storage
}
