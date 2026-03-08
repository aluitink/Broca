namespace Broca.ActivityPub.Persistence.MySql.Entities;

public class ActorEntity
{
    public string Username { get; set; } = string.Empty;
    public string? ActorId { get; set; }
    public bool IsLocal { get; set; }
    public string? Domain { get; set; }
    public string ActorJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FollowEntity> Follows { get; set; } = new List<FollowEntity>();
    public ICollection<ActivityEntity> Activities { get; set; } = new List<ActivityEntity>();
    public ICollection<CollectionDefinitionEntity> CollectionDefinitions { get; set; } = new List<CollectionDefinitionEntity>();
    public ICollection<CollectionItemEntity> CollectionItems { get; set; } = new List<CollectionItemEntity>();
    public ICollection<BlobEntity> Blobs { get; set; } = new List<BlobEntity>();
    public ICollection<DeliveryQueueEntity> OutboundDeliveries { get; set; } = new List<DeliveryQueueEntity>();
}
