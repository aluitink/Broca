namespace Broca.ActivityPub.Persistence.EntityFramework.Entities;

/// <summary>
/// Entity for storing items in manual collections
/// </summary>
public class CollectionItemEntity
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string CollectionId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
}
