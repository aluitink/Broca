namespace Broca.ActivityPub.Persistence.FileSystem;

/// <summary>
/// Configuration options for FileSystem persistence
/// </summary>
public class FileSystemPersistenceOptions
{
    /// <summary>
    /// Root data directory path where all ActivityPub data will be stored
    /// </summary>
    public string? DataPath { get; set; }
}
