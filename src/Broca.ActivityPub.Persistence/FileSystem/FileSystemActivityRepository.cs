using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Persistence.FileSystem;

/// <summary>
/// File system implementation of activity repository
/// </summary>
public class FileSystemActivityRepository : IActivityRepository, IActivityStatistics, ISearchableActivityRepository
{
    private readonly string _dataPath;
    private readonly ILogger<FileSystemActivityRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ICollectionSearchEngine? _searchEngine;

    public FileSystemActivityRepository(
        IOptions<FileSystemPersistenceOptions> options,
        ILogger<FileSystemActivityRepository> logger,
        ICollectionSearchEngine? searchEngine = null)
    {
        _dataPath = options.Value.DataPath ?? throw new ArgumentNullException(nameof(options.Value.DataPath));
        _logger = logger;
        _searchEngine = searchEngine;
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
            var json = JsonSerializer.Serialize(activity, typeof(IObjectOrLink), _jsonOptions);
            await File.WriteAllTextAsync(activityPath, json, cancellationToken);
            await IndexActivityUnsafeAsync(activity, activityId);
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
            var json = JsonSerializer.Serialize(activity, typeof(IObjectOrLink), _jsonOptions);
            await File.WriteAllTextAsync(activityPath, json, cancellationToken);
            await IndexActivityUnsafeAsync(activity, activityId);
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
        // Filter to only return Activities (not bare Objects like Note, Article, etc.)
        // Per ActivityPub spec, outbox should only contain Activities
        return await GetActivitiesFromDirectoryAsync(GetOutboxDirectory(username), limit, offset, cancellationToken, filterToActivitiesOnly: true);
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

        // Read activity before acquiring the lock so we know which indexes to update
        var activity = await GetActivityByIdAsync(activityId, cancellationToken);

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

            if (activity != null)
                await RemoveFromIndexUnsafeAsync(activity, activityId);
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
        // Only count Activities, not bare Objects
        return await GetActivityCountAsync(GetOutboxDirectory(username), cancellationToken, filterToActivitiesOnly: true);
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

    public async Task MarkObjectAsDeletedAsync(string objectId, CancellationToken cancellationToken = default)
    {
        var tombstone = new Tombstone
        {
            Id = objectId,
            Type = new[] { "Tombstone" },
            Deleted = DateTime.UtcNow
        };

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var sanitizedId = SanitizeFileName(objectId);
            var activitiesDir = Path.Combine(_dataPath, "activities");
            
            if (Directory.Exists(activitiesDir))
            {
                var json = JsonSerializer.Serialize<IObjectOrLink>(tombstone, _jsonOptions);
                
                foreach (var userDir in Directory.GetDirectories(activitiesDir))
                {
                    foreach (var boxType in new[] { "inbox", "outbox" })
                    {
                        var boxDir = Path.Combine(userDir, boxType);
                        var activityPath = Path.Combine(boxDir, $"{sanitizedId}.json");
                        if (File.Exists(activityPath))
                        {
                            await File.WriteAllTextAsync(activityPath, json, cancellationToken);
                        }
                    }
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task RecordInteractionAsync(string objectId, ActivityInteractionType type, string activityId, CancellationToken cancellationToken = default)
    {
        var metadataKey = type switch
        {
            ActivityInteractionType.Like => "likes",
            ActivityInteractionType.Announce => "shares",
            ActivityInteractionType.Reply => "replies",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await AppendToFileListUnsafeAsync(GetObjectMetadataPath(objectId, metadataKey), activityId);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task RemoveInteractionAsync(string objectId, ActivityInteractionType type, string activityId, CancellationToken cancellationToken = default)
    {
        var metadataKey = type switch
        {
            ActivityInteractionType.Like => "likes",
            ActivityInteractionType.Announce => "shares",
            ActivityInteractionType.Reply => "replies",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await RemoveFromFileListUnsafeAsync(GetObjectMetadataPath(objectId, metadataKey), activityId);
        }
        finally
        {
            _writeLock.Release();
        }
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

    private async Task<IEnumerable<IObjectOrLink>> GetActivitiesFromDirectoryAsync(
        string directory, int limit, int offset, CancellationToken cancellationToken,
        bool filterToActivitiesOnly = false)
    {
        if (!Directory.Exists(directory))
            return Array.Empty<IObjectOrLink>();

        var files = Directory.GetFiles(directory, "*.json")
            .OrderByDescending(f => f);

        var activities = new List<IObjectOrLink>();
        var skipped = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var activity = JsonSerializer.Deserialize<IObjectOrLink>(json, _jsonOptions);

                if (activity == null) continue;
                if (filterToActivitiesOnly && activity is not Activity) continue;

                if (skipped < offset)
                {
                    skipped++;
                    continue;
                }

                activities.Add(activity);
                if (activities.Count >= limit)
                    break;
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

    private async Task<int> GetActivityCountAsync(string directory, CancellationToken cancellationToken, bool filterToActivitiesOnly)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        if (!filterToActivitiesOnly)
        {
            return Directory.GetFiles(directory, "*.json").Length;
        }

        // Count only Activities
        var files = Directory.GetFiles(directory, "*.json");
        var count = 0;

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var activity = JsonSerializer.Deserialize<IObjectOrLink>(json, _jsonOptions);
                
                if (activity is Activity)
                {
                    count++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading activity file {Path} for counting", file);
            }
        }

        return count;
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

    private async Task IndexActivityUnsafeAsync(IObjectOrLink activity, string activityId)
    {
        if (activity is not IObject obj || obj.Type == null)
            return;

        if (obj.Type.Contains("Like"))
        {
            var objectId = ExtractObjectId(obj);
            if (!string.IsNullOrEmpty(objectId))
                await AppendToFileListUnsafeAsync(GetObjectMetadataPath(objectId, "likes"), activityId);
        }
        else if (obj.Type.Contains("Announce"))
        {
            var objectId = ExtractObjectId(obj);
            if (!string.IsNullOrEmpty(objectId))
                await AppendToFileListUnsafeAsync(GetObjectMetadataPath(objectId, "shares"), activityId);
        }
        else if (TryGetInReplyTo(obj, out var inReplyTo))
        {
            await AppendToFileListUnsafeAsync(GetObjectMetadataPath(inReplyTo, "replies"), activityId);
        }
    }

    private async Task RemoveFromIndexUnsafeAsync(IObjectOrLink activity, string activityId)
    {
        if (activity is not IObject obj || obj.Type == null)
            return;

        if (obj.Type.Contains("Like"))
        {
            var objectId = ExtractObjectId(obj);
            if (!string.IsNullOrEmpty(objectId))
                await RemoveFromFileListUnsafeAsync(GetObjectMetadataPath(objectId, "likes"), activityId);
        }
        else if (obj.Type.Contains("Announce"))
        {
            var objectId = ExtractObjectId(obj);
            if (!string.IsNullOrEmpty(objectId))
                await RemoveFromFileListUnsafeAsync(GetObjectMetadataPath(objectId, "shares"), activityId);
        }
        else if (TryGetInReplyTo(obj, out var inReplyTo))
        {
            await RemoveFromFileListUnsafeAsync(GetObjectMetadataPath(inReplyTo, "replies"), activityId);
        }
    }

    private async Task AppendToFileListUnsafeAsync(string listFilePath, string activityId)
    {
        List<string> list;
        if (File.Exists(listFilePath))
        {
            var json = await File.ReadAllTextAsync(listFilePath);
            list = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();
        }
        else
        {
            list = new List<string>();
        }

        if (!list.Contains(activityId))
        {
            list.Add(activityId);
            await File.WriteAllTextAsync(listFilePath, JsonSerializer.Serialize(list, _jsonOptions));
        }
    }

    private async Task RemoveFromFileListUnsafeAsync(string listFilePath, string activityId)
    {
        if (!File.Exists(listFilePath))
            return;

        var json = await File.ReadAllTextAsync(listFilePath);
        var list = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();
        if (list.Remove(activityId))
            await File.WriteAllTextAsync(listFilePath, JsonSerializer.Serialize(list, _jsonOptions));
    }

    private static string? ExtractObjectId(IObject obj)
    {
        var first = (obj as Activity)?.Object?.FirstOrDefault();
        return first switch
        {
            IObject o when !string.IsNullOrEmpty(o.Id) => o.Id,
            ILink l when l.Href != null => l.Href.ToString(),
            _ => null
        };
    }

    private static bool TryGetInReplyTo(IObject obj, out string inReplyTo)
    {
        inReplyTo = string.Empty;
        var first = obj.InReplyTo?.FirstOrDefault();
        switch (first)
        {
            case IObject o when !string.IsNullOrEmpty(o.Id):
                inReplyTo = o.Id;
                return true;
            case ILink l when l.Href != null:
                inReplyTo = l.Href.ToString()!;
                return true;
            default:
                return false;
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

    public async Task<int> CountCreateActivitiesSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        var activitiesDir = Path.Combine(_dataPath, "activities");
        if (!Directory.Exists(activitiesDir))
        {
            return 0;
        }

        var count = 0;
        try
        {
            var userDirs = Directory.GetDirectories(activitiesDir);
            foreach (var userDir in userDirs)
            {
                var outboxDir = Path.Combine(userDir, "outbox");
                if (!Directory.Exists(outboxDir))
                {
                    continue;
                }

                var files = Directory.GetFiles(outboxDir, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        // Use file creation time as a quick filter
                        var fileTime = File.GetLastWriteTimeUtc(file);
                        if (fileTime < since)
                        {
                            continue;
                        }

                        // Read and check if it's a Create activity
                        var json = await File.ReadAllTextAsync(file, cancellationToken);
                        var activity = JsonSerializer.Deserialize<IObjectOrLink>(json, _jsonOptions);
                        
                        if (activity is Create)
                        {
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error reading activity file {Path} for counting", file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting Create activities");
        }

        return count;
    }

    public async Task<int> CountActiveActorsSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        var activitiesDir = Path.Combine(_dataPath, "activities");
        if (!Directory.Exists(activitiesDir))
        {
            return 0;
        }

        var activeUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var userDirs = Directory.GetDirectories(activitiesDir);
            foreach (var userDir in userDirs)
            {
                var username = Path.GetFileName(userDir);
                var outboxDir = Path.Combine(userDir, "outbox");
                if (!Directory.Exists(outboxDir))
                {
                    continue;
                }

                var files = Directory.GetFiles(outboxDir, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        // Use file creation time as a quick filter
                        var fileTime = File.GetLastWriteTimeUtc(file);
                        if (fileTime < since)
                        {
                            continue;
                        }

                        // Read and check if it's a Create activity
                        var json = await File.ReadAllTextAsync(file, cancellationToken);
                        var activity = JsonSerializer.Deserialize<IObjectOrLink>(json, _jsonOptions);
                        
                        if (activity is Create)
                        {
                            activeUsernames.Add(username);
                            break; // This user is active, no need to check more files
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error reading activity file {Path} for counting", file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting active actors");
        }

        return activeUsernames.Count;
    }

    public async Task<IEnumerable<IObjectOrLink>> GetInboxActivitiesAsync(
        string username,
        CollectionSearchParameters search,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var all = await GetInboxActivitiesAsync(username, int.MaxValue, 0, cancellationToken);
        return ApplySearch(all, search, limit, offset);
    }

    public async Task<int> GetInboxCountAsync(
        string username,
        CollectionSearchParameters search,
        CancellationToken cancellationToken = default)
    {
        var all = await GetInboxActivitiesAsync(username, int.MaxValue, 0, cancellationToken);
        return ApplySearchCount(all, search);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetOutboxActivitiesAsync(
        string username,
        CollectionSearchParameters search,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var all = await GetOutboxActivitiesAsync(username, int.MaxValue, 0, cancellationToken);
        return ApplySearch(all, search, limit, offset);
    }

    public async Task<int> GetOutboxCountAsync(
        string username,
        CollectionSearchParameters search,
        CancellationToken cancellationToken = default)
    {
        var all = await GetOutboxActivitiesAsync(username, int.MaxValue, 0, cancellationToken);
        return ApplySearchCount(all, search);
    }

    public async Task<IEnumerable<IObjectOrLink>> GetRepliesAsync(
        string objectId,
        CollectionSearchParameters search,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var all = await GetRepliesAsync(objectId, int.MaxValue, 0, cancellationToken);
        return ApplySearch(all, search, limit, offset);
    }

    public async Task<int> GetRepliesCountAsync(
        string objectId,
        CollectionSearchParameters search,
        CancellationToken cancellationToken = default)
    {
        var all = await GetRepliesAsync(objectId, int.MaxValue, 0, cancellationToken);
        return ApplySearchCount(all, search);
    }

    private IEnumerable<IObjectOrLink> ApplySearch(
        IEnumerable<IObjectOrLink> items,
        CollectionSearchParameters search,
        int limit,
        int offset)
    {
        if (_searchEngine == null)
            return items.Skip(offset).Take(limit);

        var (filtered, _) = _searchEngine.Apply(items, search);
        return filtered.Skip(offset).Take(limit);
    }

    private int ApplySearchCount(IEnumerable<IObjectOrLink> items, CollectionSearchParameters search)
    {
        if (_searchEngine == null)
            return items.Count();

        var (_, count) = _searchEngine.Apply(items, search);
        return count;
    }
}
