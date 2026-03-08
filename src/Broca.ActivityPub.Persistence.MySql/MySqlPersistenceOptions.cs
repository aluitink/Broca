namespace Broca.ActivityPub.Persistence.MySql;

public class MySqlPersistenceOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
}
