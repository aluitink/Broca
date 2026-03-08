using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Broca.ActivityPub.Persistence.MySql.Entities;

[Table("collections")]
public class CollectionEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public long ActorId { get; set; }

    [MaxLength(64)]
    public CollectionType Type { get; set; }

    [MaxLength(767)]
    public string? TargetUri { get; set; }

    [MaxLength(255)]
    public string? Name { get; set; }

    [Column(TypeName = "longtext")]
    public string? DefinitionJson { get; set; }

    public ActorEntity Actor { get; set; } = null!;

    public List<CollectionMemberEntity> Members { get; set; } = new();
}

public enum CollectionType
{
    Inbox,
    Outbox,
    Likes,
    Shares,
    Replies,
    Custom
}
