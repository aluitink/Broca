using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Broca.ActivityPub.Persistence.MySql.Entities;

[Table("activities")]
public class ActivityEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [MaxLength(767)]
    public string ActivityUri { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? ActivityType { get; set; }

    [Column(TypeName = "longtext")]
    public string ActivityJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<CollectionMemberEntity> CollectionMembers { get; set; } = new();
}
