using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Broca.ActivityPub.Persistence.MySql.Entities;

[Table("actors")]
public class ActorEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [MaxLength(255)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(767)]
    public string? ActorUri { get; set; }

    [Column(TypeName = "longtext")]
    public string ActorJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<CollectionEntity> Collections { get; set; } = new();

    public List<ActorRelationshipEntity> Relationships { get; set; } = new();

    public List<BlobEntity> Blobs { get; set; } = new();
}
