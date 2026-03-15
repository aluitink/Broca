using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Broca.ActivityPub.Persistence.MySql.Entities;

[Table("actor_relationships")]
public class ActorRelationshipEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public long ActorId { get; set; }

    [MaxLength(766)]
    public string TargetActorUri { get; set; } = string.Empty;

    public ActorRelationshipFlags Flags { get; set; } = ActorRelationshipFlags.None;

    public ActorEntity Actor { get; set; } = null!;
}
