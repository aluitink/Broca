using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.FileSystem;
using Broca.ActivityPub.Persistence.InMemory;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.UnitTests;

public abstract class ActorRepositoryTests
{
    protected abstract IActorRepository CreateRepository();

    private static Person CreateTestActor(string username, string baseUrl = "https://example.com") => new()
    {
        Id = $"{baseUrl}/users/{username}",
        Type = new[] { "Person" },
        PreferredUsername = username,
        Name = new[] { username }
    };

    [Fact]
    public async Task SaveActorAsync_NewActor_CanBeRetrievedByUsername()
    {
        var repo = CreateRepository();
        var actor = CreateTestActor("alice");

        await repo.SaveActorAsync("alice", actor);

        var result = await repo.GetActorByUsernameAsync("alice");
        Assert.NotNull(result);
        Assert.Equal("alice", result.PreferredUsername);
    }

    [Fact]
    public async Task SaveActorAsync_ExistingActor_UpdatesActor()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));

        var updated = CreateTestActor("alice");
        updated.Name = new[] { "Alice Updated" };
        await repo.SaveActorAsync("alice", updated);

        var result = await repo.GetActorByUsernameAsync("alice");
        Assert.NotNull(result);
        Assert.Equal("Alice Updated", result.Name?.FirstOrDefault());
    }

    [Fact]
    public async Task GetActorByUsernameAsync_NonExistentActor_ReturnsNull()
    {
        var repo = CreateRepository();

        var result = await repo.GetActorByUsernameAsync("nobody");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActorByUsernameAsync_CaseInsensitive_ReturnsActor()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));

        var result = await repo.GetActorByUsernameAsync("ALICE");

        Assert.NotNull(result);
        Assert.Equal("alice", result.PreferredUsername);
    }

    [Fact]
    public async Task GetActorByIdAsync_ExistingActor_ReturnsActor()
    {
        var repo = CreateRepository();
        var actor = CreateTestActor("alice");
        await repo.SaveActorAsync("alice", actor);

        var result = await repo.GetActorByIdAsync("https://example.com/users/alice");

        Assert.NotNull(result);
        Assert.Equal("alice", result.PreferredUsername);
    }

    [Fact]
    public async Task GetActorByIdAsync_NonExistentId_ReturnsNull()
    {
        var repo = CreateRepository();

        var result = await repo.GetActorByIdAsync("https://example.com/users/ghost");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteActorAsync_ExistingActor_CanNoLongerBeRetrieved()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));

        await repo.DeleteActorAsync("alice");

        Assert.Null(await repo.GetActorByUsernameAsync("alice"));
    }

    [Fact]
    public async Task DeleteActorAsync_NonExistentActor_DoesNotThrow()
    {
        var repo = CreateRepository();

        await repo.DeleteActorAsync("nobody");
    }

    [Fact]
    public async Task AddFollowerAsync_AddsFollower_AppearsInGetFollowers()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));

        await repo.AddFollowerAsync("alice", "https://remote.example/users/bob");

        var followers = await repo.GetFollowersAsync("alice");
        Assert.Contains("https://remote.example/users/bob", followers);
    }

    [Fact]
    public async Task RemoveFollowerAsync_ExistingFollower_RemovesFromList()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));
        await repo.AddFollowerAsync("alice", "https://remote.example/users/bob");

        await repo.RemoveFollowerAsync("alice", "https://remote.example/users/bob");

        var followers = await repo.GetFollowersAsync("alice");
        Assert.DoesNotContain("https://remote.example/users/bob", followers);
    }

    [Fact]
    public async Task AddFollowerAsync_SameFollowerTwice_AppearsOnceInList()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));

        await repo.AddFollowerAsync("alice", "https://remote.example/users/bob");
        await repo.AddFollowerAsync("alice", "https://remote.example/users/bob");

        var followers = await repo.GetFollowersAsync("alice");
        Assert.Equal(1, followers.Count(f => f == "https://remote.example/users/bob"));
    }

    [Fact]
    public async Task GetFollowersAsync_NoFollowers_ReturnsEmpty()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));

        var followers = await repo.GetFollowersAsync("alice");

        Assert.Empty(followers);
    }

    [Fact]
    public async Task AddFollowingAsync_AddsFollowing_AppearsInGetFollowing()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));

        await repo.AddFollowingAsync("alice", "https://remote.example/users/carol");

        var following = await repo.GetFollowingAsync("alice");
        Assert.Contains("https://remote.example/users/carol", following);
    }

    [Fact]
    public async Task RemoveFollowingAsync_ExistingFollowing_RemovesFromList()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));
        await repo.AddFollowingAsync("alice", "https://remote.example/users/carol");

        await repo.RemoveFollowingAsync("alice", "https://remote.example/users/carol");

        var following = await repo.GetFollowingAsync("alice");
        Assert.DoesNotContain("https://remote.example/users/carol", following);
    }

    [Fact]
    public async Task GetFollowingAsync_NoFollowing_ReturnsEmpty()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));

        var following = await repo.GetFollowingAsync("alice");

        Assert.Empty(following);
    }

    [Fact]
    public async Task SaveCollectionDefinitionAsync_NewCollection_CanBeRetrieved()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));
        var definition = new CustomCollectionDefinition
        {
            Id = "featured",
            Name = "Featured",
            Type = CollectionType.Manual,
            Visibility = CollectionVisibility.Public
        };

        await repo.SaveCollectionDefinitionAsync("alice", definition);

        var result = await repo.GetCollectionDefinitionAsync("alice", "featured");
        Assert.NotNull(result);
        Assert.Equal("Featured", result.Name);
    }

    [Fact]
    public async Task GetCollectionDefinitionsAsync_MultipleCollections_ReturnsAll()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));
        await repo.SaveCollectionDefinitionAsync("alice", new CustomCollectionDefinition { Id = "featured", Name = "Featured", Type = CollectionType.Manual, Visibility = CollectionVisibility.Public });
        await repo.SaveCollectionDefinitionAsync("alice", new CustomCollectionDefinition { Id = "pinned", Name = "Pinned", Type = CollectionType.Manual, Visibility = CollectionVisibility.Public });

        var definitions = await repo.GetCollectionDefinitionsAsync("alice");

        Assert.Equal(2, definitions.Count());
    }

    [Fact]
    public async Task DeleteCollectionDefinitionAsync_ExistingCollection_RemovesIt()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));
        await repo.SaveCollectionDefinitionAsync("alice", new CustomCollectionDefinition { Id = "featured", Name = "Featured", Type = CollectionType.Manual, Visibility = CollectionVisibility.Public });

        await repo.DeleteCollectionDefinitionAsync("alice", "featured");

        Assert.Null(await repo.GetCollectionDefinitionAsync("alice", "featured"));
    }

    [Fact]
    public async Task AddToCollectionAsync_ManualCollection_AddsItem()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));
        await repo.SaveCollectionDefinitionAsync("alice", new CustomCollectionDefinition { Id = "featured", Name = "Featured", Type = CollectionType.Manual, Visibility = CollectionVisibility.Public });

        await repo.AddToCollectionAsync("alice", "featured", "https://example.com/notes/1");

        var definition = await repo.GetCollectionDefinitionAsync("alice", "featured");
        Assert.NotNull(definition);
        Assert.Contains("https://example.com/notes/1", definition.Items);
    }

    [Fact]
    public async Task RemoveFromCollectionAsync_ManualCollection_RemovesItem()
    {
        var repo = CreateRepository();
        await repo.SaveActorAsync("alice", CreateTestActor("alice"));
        await repo.SaveCollectionDefinitionAsync("alice", new CustomCollectionDefinition { Id = "featured", Name = "Featured", Type = CollectionType.Manual, Visibility = CollectionVisibility.Public });
        await repo.AddToCollectionAsync("alice", "featured", "https://example.com/notes/1");

        await repo.RemoveFromCollectionAsync("alice", "featured", "https://example.com/notes/1");

        var definition = await repo.GetCollectionDefinitionAsync("alice", "featured");
        Assert.NotNull(definition);
        Assert.DoesNotContain("https://example.com/notes/1", definition.Items);
    }
}

public class InMemoryActorRepositoryTests : ActorRepositoryTests
{
    protected override IActorRepository CreateRepository() => new InMemoryActorRepository();
}

public class FileSystemActorRepositoryTests : ActorRepositoryTests, IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "broca-actor-tests", Guid.NewGuid().ToString());

    protected override IActorRepository CreateRepository() =>
        new FileSystemActorRepository(
            Options.Create(new FileSystemPersistenceOptions { DataPath = _tempDir }),
            NullLogger<FileSystemActorRepository>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
