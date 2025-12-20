namespace Broca.ActivityPub.Persistence.EntityFramework.Entities;

/// <summary>
/// Entity for storing custom collection definitions
/// </summary>
public class CollectionDefinitionEntity
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string CollectionId { get; set; } = string.Empty;
    public string DefinitionJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
