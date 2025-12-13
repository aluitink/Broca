using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Persistence.FileSystem;

/// <summary>
/// File system implementation of activity repository
/// </summary>
public class FileSystemActivityRepository : IActivityRepository
{
    private readonly string _dataPath;
    private readonly ILogger<FileSystemActivityRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public FileSystemActivityRepository(
        IOptions<FileSystemPersistenceOptions> options,
        ILogger<FileSystemActivityRepository> logger)
    {
        _dataPath = options.Value.DataPath ?? throw new ArgumentNullException(nameof(options.Value.DataPath));
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        EnsureDirectoryExists(Path.Combine(_dataPath, "activities"));
        EnsureDirectoryExists(Path.Combine(_dataPath, "objects"));
    }

    public async Task SaveInboxActivityAsync(string username, string activityId, IObjectOrLink activity, CancellationToken cancellationToken = default)
    {
        var inboxDir = GetInboxDirectory(username);
        EnsureDirectoryExists(inboxDir);

        var activityPath = Path.Combine(inboxDir, $"{SanitizeFileName(activityId)}.json");

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(activity, activity.GetType(), _jsonOptions);
            await File.WriteAllTextAsync(activityPath, json, cancellationToken);
            _logger.LogInformation("Saved inbox activity {ActivityId} for {Username}", activityId, username);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveOutboxActivityAsync(string username, string activityId, IObjectOrLink activity, CancellationToken cancellationToken = default)
    {
        var outboxDir = GetOutboxDirectory(username);
        EnsureDirectoryExists(outboxDir);

        var activityPath = Path.Combine(outboxDir, $"{SanitizeFileName(activityId)}.json");

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(activity, activity.GetType(), _jsonOptions);
            await File.WriteAllTextAsync(activityPath, json, cancellationToken);
            _logger.LogInformation("Saved outbox activity {ActivityId} for {Username}", activityId, username);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IEnumerable<IObjectOrLink>> GetInboxActivitiesAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        return await GetActivitiesFromDirectoryAsync(GetInboxDirectory(username), limit, offset, cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetOutboxActivitiesAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        return await GetActivitiesFromDirectoryAsync(GetOutboxDirectory(username), limit, offset, cancellationToken);
    }

    public async Task<IObjectOrLink?> GetActivityByIdAsync(string activityId, CancellationToken cancellationToken = default)
    {
        // Search in all inbox/outbox directories
        var activitiesDir = Path.Combine(_dataPath, "activities");
        if (!Directory.Exists(activitiesDir))
        {
            return null;
        }

        var sanitizedId = SanitizeFileName(activityId);
        foreach (var userDir in Directory.GetDirectories(activitiesDir))
        {
            foreach (var boxType in new[] { "inbox", "outbox" })
            {
                var boxDir = Path.Combine(userDir, boxType);
                if (!Directory.Exists(boxDir))
                {
                    continue;
                }

                var activityPath = Path.Combine(boxDir, $"{sanitizedId}.json");
                if (File.Exists(activityPath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(activityPath, cancellationToken);
                        return JsonSerializer.Deserialize<IObjectOrLink>(json, _jsonOptions);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error reading activity {ActivityId} from {Path}", activityId, activityPath);
                    }
                }
            }
        }

        return null;
    }

    public async Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
    {
        var activitiesDir = Path.Combine(_dataPath, "activities");
        if (!Directory.Exists(activitiesDir))
        {
            return;
        }

        var sanitizedId = SanitizeFileName(activityId);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var userDir in Directory.GetDirectories(activitiesDir))
            {
                foreach (var boxType in new[] { "inbox", "outbox" })
                {
                    var boxDir = Path.Combine(userDir, boxType);
                    var activityPath = Path.Combine(boxDir, $"{sanitizedId}.json");
                    if (File.Exists(activityPath))
                    {
                        File.Delete(activityPath);
                        _logger.LogInformation("Deleted activity {ActivityId}", activityId);
                    }
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<int> GetInboxCountAsync(string username, CancellationToken cancellationToken = default)
    {
        return await GetActivityCountAsync(GetInboxDirectory(username), cancellationToken);
    }

    public async Task<int> GetOutboxCountAsync(string username, CancellationToken cancellationToken = default)
    {
        return await GetActivityCountAsync(GetOutboxDirectory(username), cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetRepliesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        var repliesPath = GetObjectMetadataPath(objectId, "replies");
        return await GetActivitiesFromFileListAsync(repliesPath, limit, offset, cancellationToken);
    }

    public async Task<int> GetRepliesCountAsync(string objectId, CancellationToken cancellationToken = default)
    {
        return await GetObjectMetadataCountAsync(objectId, "replies", cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetLikesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        var likesPath = GetObjectMetadataPath(objectId, "likes");
        return await GetActivitiesFromFileListAsync(likesPath, limit, offset, cancellationToken);
    }

    public async Task<int> GetLikesCountAsync(string objectId, CancellationToken cancellationToken = default)
    {
        return await GetObjectMetadataCountAsync(objectId, "likes", cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetSharesAsync(string objectId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        var sharesPath = GetObjectMetadataPath(objectId, "shares");
        return await GetActivitiesFromFileListAsync(sharesPath, limit, offset, cancellationToken);
    }

    public async Task<int> GetSharesCountAsync(string objectId, CancellationToken cancellationToken = default)
    {
        return await GetObjectMetadataCountAsync(objectId, "shares", cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetLikedByActorAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        var likedPath = GetUserMetadataPath(username, "liked");
        return await GetActivitiesFromFileListAsync(likedPath, limit, offset, cancellationToken);
    }

    public async Task<int> GetLikedByActorCountAsync(string username, CancellationToken cancellationToken = default)
    {
        return await GetUserMetadataCountAsync(username, "liked", cancellationToken);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetSharedByActorAsync(string username, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        var sharedPath = GetUserMetadataPath(username, "shared");
        return await GetActivitiesFromFileListAsync(sharedPath, limit, offset, cancellationToken);
    }

    public async Task<int> GetSharedByActorCountAsync(string username, CancellationToken cancellationToken = default)
    {
        return await GetUserMetadataCountAsync(username, "shared", cancellationToken);
    }

    // Helper methods

    private string GetUserActivityDirectory(string username)
    {
        return Path.Combine(_dataPath, "activities", username.ToLowerInvariant());
    }

    private string GetInboxDirectory(string username)
    {
        return Path.Combine(GetUserActivityDirectory(username), "inbox");
    }

    private string GetOutboxDirectory(string username)
    {
        return Path.Combine(GetUserActivityDirectory(username), "outbox");
    }

    private string GetObjectMetadataPath(string objectId, string metadataType)
    {
        var objectDir = Path.Combine(_dataPath, "objects", SanitizeFileName(objectId));
        EnsureDirectoryExists(objectDir);
        return Path.Combine(objectDir, $"{metadataType}.json");
    }

    private string GetUserMetadataPath(string username, string metadataType)
    {
        var userDir = GetUserActivityDirectory(username);
        EnsureDirectoryExists(userDir);
        return Path.Combine(userDir, $"{metadataType}.json");
    }

    private async Task<IEnumerable<IObjectOrLink>> GetActivitiesFromDirectoryAsync(string directory, int limit, int offset, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return Array.Empty<IObjectOrLink>();
        }

        var files = Directory.GetFiles(directory, "*.json")
            .OrderByDescending(f => File.GetCreationTimeUtc(f))
            .Skip(offset)
            .Take(limit);

        var activities = new List<IObjectOrLink>();
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var activity = JsonSerializer.Deserialize<IObjectOrLink>(json, _jsonOptions);
                if (activity != null)
                {
                    activities.Add(activity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading activity file {Path}", file);
            }
        }

        return activities;
    }

    private async Task<IEnumerable<IObjectOrLink>> GetActivitiesFromFileListAsync(string listFilePath, int limit, int offset, CancellationToken cancellationToken)
    {
        if (!File.Exists(listFilePath))
        {
            return Array.Empty<IObjectOrLink>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(listFilePath, cancellationToken);
            var activityIds = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();

            var activities = new List<IObjectOrLink>();
            foreach (var activityId in activityIds.Skip(offset).Take(limit))
            {
                var activity = await GetActivityByIdAsync(activityId, cancellationToken);
                if (activity != null)
                {
                    activities.Add(activity);
                }
            }

            return activities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading activity list from {Path}", listFilePath);
            return Array.Empty<IObjectOrLink>();
        }
    }

    private async Task<int> GetActivityCountAsync(string directory, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return Directory.GetFiles(directory, "*.json").Length;
    }

    private async Task<int> GetObjectMetadataCountAsync(string objectId, string metadataType, CancellationToken cancellationToken)
    {
        var path = GetObjectMetadataPath(objectId, metadataType);
        if (!File.Exists(path))
        {
            return 0;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var list = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();
            return list.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading metadata count for {ObjectId}/{MetadataType}", objectId, metadataType);
            return 0;
        }
    }

    private async Task<int> GetUserMetadataCountAsync(string username, string metadataType, CancellationToken cancellationToken)
    {
        var path = GetUserMetadataPath(username, metadataType);
        if (!File.Exists(path))
        {
            return 0;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var list = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();
            return list.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading metadata count for {Username}/{MetadataType}", username, metadataType);
            return 0;
        }
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        // Replace characters that aren't valid in filenames
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        
        // Also replace some problematic characters
        sanitized = sanitized.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
        
        // Limit length
        if (sanitized.Length > 200)
        {
            sanitized = sanitized.Substring(0, 200);
        }
        
        return sanitized;
    }
}
