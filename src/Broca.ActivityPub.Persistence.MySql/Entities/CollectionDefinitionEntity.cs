namespace Broca.ActivityPub.Persistence.MySql.Entities;

public class CollectionDefinitionEntity
{
    public string Username { get; set; } = string.Empty;
    public string CollectionId { get; set; } = string.Empty;
    public string DefinitionJson { get; set; } = string.Empty;

    public ActorEntity? Actor { get; set; }
    public ICollection<CollectionItemEntity> Items { get; set; } = new List<CollectionItemEntity>();
}
