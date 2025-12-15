using System.Collections.Concurrent;
using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Persistence.FileSystem;

/// <summary>
/// File system implementation of actor repository
/// </summary>
public class FileSystemActorRepository : IActorRepository
{
    private readonly string _dataPath;
    private readonly ILogger<FileSystemActorRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public FileSystemActorRepository(
        IOptions<FileSystemPersistenceOptions> options,
        ILogger<FileSystemActorRepository> logger)
    {
        _dataPath = options.Value.DataPath ?? throw new ArgumentNullException(nameof(options.Value.DataPath));
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        EnsureDirectoryExists(Path.Combine(_dataPath, "actors"));
    }

    public async Task<Actor?> GetActorByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var actorPath = GetActorFilePath(username);
        if (!File.Exists(actorPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(actorPath, cancellationToken);
            return JsonSerializer.Deserialize<Actor>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading actor {Username} from {Path}", username, actorPath);
            throw;
        }
    }

    public async Task<Actor?> GetActorByIdAsync(string actorId, CancellationToken cancellationToken = default)
    {
        // Search through all actor files to find matching ID
        var actorsDir = Path.Combine(_dataPath, "actors");
        if (!Directory.Exists(actorsDir))
        {
            return null;
        }

        foreach (var userDir in Directory.GetDirectories(actorsDir))
        {
            var actorPath = Path.Combine(userDir, "actor.json");
            if (File.Exists(actorPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(actorPath, cancellationToken);
                    var actor = JsonSerializer.Deserialize<Actor>(json, _jsonOptions);
                    if (actor?.Id == actorId)
                    {
                        return actor;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading actor file {Path}", actorPath);
                }
            }
        }

        return null;
    }

    public async Task SaveActorAsync(string username, Actor actor, CancellationToken cancellationToken = default)
    {
        var userDir = GetUserDirectory(username);
        EnsureDirectoryExists(userDir);

        var actorPath = Path.Combine(userDir, "actor.json");

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(actor, _jsonOptions);
            await File.WriteAllTextAsync(actorPath, json, cancellationToken);
            _logger.LogInformation("Saved actor {Username} to {Path}", username, actorPath);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteActorAsync(string username, CancellationToken cancellationToken = default)
    {
        var userDir = GetUserDirectory(username);
        if (!Directory.Exists(userDir))
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.Delete(userDir, recursive: true);
            _logger.LogInformation("Deleted actor {Username} from {Path}", username, userDir);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IEnumerable<string>> GetFollowersAsync(string username, CancellationToken cancellationToken = default)
    {
        var followersPath = GetFollowersFilePath(username);
        if (!File.Exists(followersPath))
        {
            return Array.Empty<string>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(followersPath, cancellationToken);
            return JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading followers for {Username}", username);
            return Array.Empty<string>();
        }
    }

    public async Task<IEnumerable<string>> GetFollowingAsync(string username, CancellationToken cancellationToken = default)
    {
        var followingPath = GetFollowingFilePath(username);
        if (!File.Exists(followingPath))
        {
            return Array.Empty<string>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(followingPath, cancellationToken);
            return JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading following for {Username}", username);
            return Array.Empty<string>();
        }
    }

    public async Task AddFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
    {
        await AddToListAsync(GetFollowersFilePath(username), followerActorId, cancellationToken);
        _logger.LogInformation("Added follower {FollowerId} to {Username}", followerActorId, username);
    }

    public async Task RemoveFollowerAsync(string username, string followerActorId, CancellationToken cancellationToken = default)
    {
        await RemoveFromListAsync(GetFollowersFilePath(username), followerActorId, cancellationToken);
        _logger.LogInformation("Removed follower {FollowerId} from {Username}", followerActorId, username);
    }

    public async Task AddFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
    {
        await AddToListAsync(GetFollowingFilePath(username), followingActorId, cancellationToken);
        _logger.LogInformation("Added following {FollowingId} to {Username}", followingActorId, username);
    }

    public async Task RemoveFollowingAsync(string username, string followingActorId, CancellationToken cancellationToken = default)
    {
        await RemoveFromListAsync(GetFollowingFilePath(username), followingActorId, cancellationToken);
        _logger.LogInformation("Removed following {FollowingId} from {Username}", followingActorId, username);
    }

    // Helper methods

    private string GetUserDirectory(string username)
    {
        return Path.Combine(_dataPath, "actors", username.ToLowerInvariant());
    }

    private string GetActorFilePath(string username)
    {
        return Path.Combine(GetUserDirectory(username), "actor.json");
    }

    private string GetFollowersFilePath(string username)
    {
        return Path.Combine(GetUserDirectory(username), "followers.json");
    }

    private string GetFollowingFilePath(string username)
    {
        return Path.Combine(GetUserDirectory(username), "following.json");
    }

    private string GetCollectionsDirectory(string username)
    {
        return Path.Combine(GetUserDirectory(username), "collections");
    }

    private string GetCollectionFilePath(string username, string collectionId)
    {
        return Path.Combine(GetCollectionsDirectory(username), $"{collectionId}.json");
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private async Task AddToListAsync(string filePath, string item, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var list = new List<string>();
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                list = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();
            }

            if (!list.Contains(item))
            {
                list.Add(item);
                var json = JsonSerializer.Serialize(list, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json, cancellationToken);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task RemoveFromListAsync(string filePath, string item, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var list = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();

            if (list.Remove(item))
            {
                json = JsonSerializer.Serialize(list, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json, cancellationToken);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // Custom Collections

    public async Task<IEnumerable<CustomCollectionDefinition>> GetCollectionDefinitionsAsync(string username, CancellationToken cancellationToken = default)
    {
        var collectionsDir = GetCollectionsDirectory(username);
        if (!Directory.Exists(collectionsDir))
        {
            return Array.Empty<CustomCollectionDefinition>();
        }

        try
        {
            var definitions = new List<CustomCollectionDefinition>();
            foreach (var file in Directory.GetFiles(collectionsDir, "*.json"))
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var definition = JsonSerializer.Deserialize<CustomCollectionDefinition>(json, _jsonOptions);
                if (definition != null)
                {
                    definitions.Add(definition);
                }
            }
            return definitions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading collection definitions for {Username}", username);
            return Array.Empty<CustomCollectionDefinition>();
        }
    }

    public async Task<CustomCollectionDefinition?> GetCollectionDefinitionAsync(string username, string collectionId, CancellationToken cancellationToken = default)
    {
        var filePath = GetCollectionFilePath(username, collectionId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<CustomCollectionDefinition>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading collection {CollectionId} for {Username}", collectionId, username);
            return null;
        }
    }

    public async Task SaveCollectionDefinitionAsync(string username, CustomCollectionDefinition definition, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var collectionsDir = GetCollectionsDirectory(username);
            EnsureDirectoryExists(collectionsDir);

            var filePath = GetCollectionFilePath(username, definition.Id);
            var json = JsonSerializer.Serialize(definition, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger.LogInformation("Saved collection definition {CollectionId} for {Username}", definition.Id, username);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteCollectionDefinitionAsync(string username, string collectionId, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetCollectionFilePath(username, collectionId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted collection {CollectionId} for {Username}", collectionId, username);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task AddToCollectionAsync(string username, string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var definition = await GetCollectionDefinitionAsync(username, collectionId, cancellationToken);
            if (definition == null)
            {
                throw new InvalidOperationException($"Collection {collectionId} not found");
            }

            if (definition.Type != CollectionType.Manual)
            {
                throw new InvalidOperationException($"Cannot manually add items to query collection {collectionId}");
            }

            if (!definition.Items.Contains(itemId))
            {
                definition.Items.Add(itemId);
                definition.Updated = DateTimeOffset.UtcNow;
                await SaveCollectionDefinitionAsync(username, definition, cancellationToken);
                _logger.LogInformation("Added item {ItemId} to collection {CollectionId} for {Username}", itemId, collectionId, username);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task RemoveFromCollectionAsync(string username, string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var definition = await GetCollectionDefinitionAsync(username, collectionId, cancellationToken);
            if (definition == null)
            {
                return;
            }

            if (definition.Type != CollectionType.Manual)
            {
                throw new InvalidOperationException($"Cannot manually remove items from query collection {collectionId}");
            }

            if (definition.Items.Remove(itemId))
            {
                definition.Updated = DateTimeOffset.UtcNow;
                await SaveCollectionDefinitionAsync(username, definition, cancellationToken);
                _logger.LogInformation("Removed item {ItemId} from collection {CollectionId} for {Username}", itemId, collectionId, username);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
