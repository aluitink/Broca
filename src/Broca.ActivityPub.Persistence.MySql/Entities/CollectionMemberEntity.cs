using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Broca.ActivityPub.Persistence.MySql.Entities;

[Table("collection_members")]
public class CollectionMemberEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public long CollectionId { get; set; }

    public long ActivityId { get; set; }

    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    public CollectionEntity Collection { get; set; } = null!;

    public ActivityEntity Activity { get; set; } = null!;
}
