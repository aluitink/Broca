namespace Broca.ActivityPub.Persistence.FileSystem;

public class FileSystemPersistenceOptions
{
    public string? DataPath { get; set; }

    public int DeadLetterRetentionDays { get; set; } = 30;

    public int DeliveredItemRetentionHours { get; set; } = 24;
}
