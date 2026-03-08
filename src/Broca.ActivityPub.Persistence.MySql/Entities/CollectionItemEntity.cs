namespace Broca.ActivityPub.Persistence.MySql.Entities;

public class CollectionItemEntity
{
    public string Username { get; set; } = string.Empty;
    public string CollectionId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;

    public ActorEntity? Actor { get; set; }
    public CollectionDefinitionEntity? CollectionDefinition { get; set; }
}
