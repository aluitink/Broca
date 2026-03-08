using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Broca.ActivityPub.Persistence.MySql.Entities;

[Table("blobs")]
public class BlobEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public long ActorId { get; set; }

    [MaxLength(512)]
    public string BlobId { get; set; } = string.Empty;

    [Column(TypeName = "longblob")]
    public byte[] Content { get; set; } = Array.Empty<byte>();

    [MaxLength(255)]
    public string ContentType { get; set; } = "application/octet-stream";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ActorEntity Actor { get; set; } = null!;
}
