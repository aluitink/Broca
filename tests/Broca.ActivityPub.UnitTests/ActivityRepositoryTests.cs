using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Persistence.FileSystem;
using Broca.ActivityPub.Persistence.InMemory;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.UnitTests;

public abstract class ActivityRepositoryTests
{
    protected abstract IActivityRepository CreateRepository();

    protected static Create CreateActivity(string activityId, string noteId, string content = "Hello world") => new()
    {
        Id = activityId,
        Type = new[] { "Create" },
        Object = new IObjectOrLink[]
        {
            new Note
            {
                Id = noteId,
                Type = new[] { "Note" },
                Content = new[] { content }
            }
        }
    };

    [Fact]
    public async Task SaveInboxActivityAsync_Activity_AppearsInInbox()
    {
        var repo = CreateRepository();
        var activity = CreateActivity("https://example.com/activities/1", "https://example.com/notes/1");

        await repo.SaveInboxActivityAsync("alice", "https://example.com/activities/1", activity);

        var inbox = await repo.GetInboxActivitiesAsync("alice");
        Assert.Single(inbox);
    }

    [Fact]
    public async Task SaveOutboxActivityAsync_Activity_AppearsInOutbox()
    {
        var repo = CreateRepository();
        var activity = CreateActivity("https://example.com/activities/2", "https://example.com/notes/2");

        await repo.SaveOutboxActivityAsync("alice", "https://example.com/activities/2", activity);

        var outbox = await repo.GetOutboxActivitiesAsync("alice");
        Assert.Single(outbox);
    }

    [Fact]
    public async Task SaveOutboxActivityAsync_BareNote_DoesNotAppearInOutbox()
    {
        var repo = CreateRepository();
        var note = new Note
        {
            Id = "https://example.com/notes/bare",
            Type = new[] { "Note" },
            Content = new[] { "bare note" }
        };

        await repo.SaveOutboxActivityAsync("alice", "https://example.com/notes/bare", note);

        var outbox = await repo.GetOutboxActivitiesAsync("alice");
        Assert.Empty(outbox);
    }

    [Fact]
    public async Task GetInboxCountAsync_AfterSavingActivities_ReturnsCorrectCount()
    {
        var repo = CreateRepository();
        await repo.SaveInboxActivityAsync("alice", "https://example.com/activities/1", CreateActivity("https://example.com/activities/1", "https://example.com/notes/1"));
        await repo.SaveInboxActivityAsync("alice", "https://example.com/activities/2", CreateActivity("https://example.com/activities/2", "https://example.com/notes/2"));

        var count = await repo.GetInboxCountAsync("alice");

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetOutboxCountAsync_AfterSavingActivities_ReturnsCorrectCount()
    {
        var repo = CreateRepository();
        await repo.SaveOutboxActivityAsync("alice", "https://example.com/activities/1", CreateActivity("https://example.com/activities/1", "https://example.com/notes/1"));
        await repo.SaveOutboxActivityAsync("alice", "https://example.com/activities/2", CreateActivity("https://example.com/activities/2", "https://example.com/notes/2"));

        var count = await repo.GetOutboxCountAsync("alice");

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetInboxCountAsync_EmptyInbox_ReturnsZero()
    {
        var repo = CreateRepository();

        var count = await repo.GetInboxCountAsync("alice");

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetInboxActivitiesAsync_EmptyInbox_ReturnsEmpty()
    {
        var repo = CreateRepository();

        var inbox = await repo.GetInboxActivitiesAsync("alice");

        Assert.Empty(inbox);
    }

    [Fact]
    public async Task GetInboxActivitiesAsync_WithLimit_ReturnsLimitedResults()
    {
        var repo = CreateRepository();
        for (var i = 1; i <= 5; i++)
        {
            await repo.SaveInboxActivityAsync("alice",
                $"https://example.com/activities/{i}",
                CreateActivity($"https://example.com/activities/{i}", $"https://example.com/notes/{i}"));
        }

        var inbox = await repo.GetInboxActivitiesAsync("alice", limit: 3);

        Assert.Equal(3, inbox.Count());
    }

    [Fact]
    public async Task GetInboxActivitiesAsync_DifferentUsers_AreIsolated()
    {
        var repo = CreateRepository();
        await repo.SaveInboxActivityAsync("alice", "https://example.com/activities/1", CreateActivity("https://example.com/activities/1", "https://example.com/notes/1"));
        await repo.SaveInboxActivityAsync("bob", "https://example.com/activities/2", CreateActivity("https://example.com/activities/2", "https://example.com/notes/2"));

        var aliceInbox = await repo.GetInboxActivitiesAsync("alice");
        var bobInbox = await repo.GetInboxActivitiesAsync("bob");

        Assert.Single(aliceInbox);
        Assert.Single(bobInbox);
    }

    [Fact]
    public async Task DeleteActivityAsync_ExistingActivity_ReducesInboxCount()
    {
        var repo = CreateRepository();
        await repo.SaveInboxActivityAsync("alice", "https://example.com/activities/1", CreateActivity("https://example.com/activities/1", "https://example.com/notes/1"));
        await repo.SaveInboxActivityAsync("alice", "https://example.com/activities/2", CreateActivity("https://example.com/activities/2", "https://example.com/notes/2"));

        await repo.DeleteActivityAsync("https://example.com/activities/1");

        Assert.Equal(1, await repo.GetInboxCountAsync("alice"));
    }

    [Fact]
    public async Task GetActivityByIdAsync_ExistingActivity_ReturnsActivity()
    {
        var repo = CreateRepository();
        var activity = CreateActivity("https://example.com/activities/99", "https://example.com/notes/99");
        await repo.SaveInboxActivityAsync("alice", "https://example.com/activities/99", activity);

        var result = await repo.GetActivityByIdAsync("https://example.com/activities/99");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetActivityByIdAsync_NonExistentId_ReturnsNull()
    {
        var repo = CreateRepository();

        var result = await repo.GetActivityByIdAsync("https://example.com/activities/ghost");

        Assert.Null(result);
    }
}

public abstract class InMemoryOnlyActivityRepositoryTests : ActivityRepositoryTests
{
    [Fact]
    public async Task GetLikesCountAsync_AfterLikeActivity_ReturnsCount()
    {
        var repo = CreateRepository();
        var noteId = "https://example.com/notes/1";
        var like = new Like
        {
            Id = "https://example.com/likes/1",
            Type = new[] { "Like" },
            Object = new IObjectOrLink[] { new Note { Id = noteId, Type = new[] { "Note" } } },
            Actor = new IObjectOrLink[] { new Link { Href = new Uri("https://example.com/users/bob") } }
        };

        await repo.SaveInboxActivityAsync("alice", like.Id!, like);

        Assert.Equal(1, await repo.GetLikesCountAsync(noteId));
    }

    [Fact]
    public async Task GetLikesAsync_AfterLikeActivity_ReturnsLike()
    {
        var repo = CreateRepository();
        var noteId = "https://example.com/notes/1";
        var like = new Like
        {
            Id = "https://example.com/likes/1",
            Type = new[] { "Like" },
            Object = new IObjectOrLink[] { new Note { Id = noteId, Type = new[] { "Note" } } },
            Actor = new IObjectOrLink[] { new Link { Href = new Uri("https://example.com/users/bob") } }
        };

        await repo.SaveInboxActivityAsync("alice", like.Id!, like);

        var likes = await repo.GetLikesAsync(noteId);
        Assert.Single(likes);
    }

    [Fact]
    public async Task GetSharesCountAsync_AfterAnnounceActivity_ReturnsCount()
    {
        var repo = CreateRepository();
        var noteId = "https://example.com/notes/1";
        var announce = new Announce
        {
            Id = "https://example.com/announces/1",
            Type = new[] { "Announce" },
            Object = new IObjectOrLink[] { new Note { Id = noteId, Type = new[] { "Note" } } },
            Actor = new IObjectOrLink[] { new Link { Href = new Uri("https://example.com/users/bob") } }
        };

        await repo.SaveInboxActivityAsync("alice", announce.Id!, announce);

        Assert.Equal(1, await repo.GetSharesCountAsync(noteId));
    }

    [Fact]
    public async Task GetSharesAsync_AfterAnnounceActivity_ReturnsShare()
    {
        var repo = CreateRepository();
        var noteId = "https://example.com/notes/1";
        var announce = new Announce
        {
            Id = "https://example.com/announces/1",
            Type = new[] { "Announce" },
            Object = new IObjectOrLink[] { new Note { Id = noteId, Type = new[] { "Note" } } },
            Actor = new IObjectOrLink[] { new Link { Href = new Uri("https://example.com/users/bob") } }
        };

        await repo.SaveInboxActivityAsync("alice", announce.Id!, announce);

        var shares = await repo.GetSharesAsync(noteId);
        Assert.Single(shares);
    }

    [Fact]
    public async Task GetRepliesCountAsync_AfterReplyNote_ReturnsCount()
    {
        var repo = CreateRepository();
        var originalId = "https://example.com/notes/original";
        var reply = new Note
        {
            Id = "https://example.com/notes/reply1",
            Type = new[] { "Note" },
            Content = new[] { "reply content" },
            InReplyTo = new IObjectOrLink[] { new Link { Href = new Uri(originalId) } }
        };

        await repo.SaveInboxActivityAsync("alice", reply.Id!, reply);

        Assert.Equal(1, await repo.GetRepliesCountAsync(originalId));
    }

    [Fact]
    public async Task GetRepliesAsync_AfterReplyNote_ReturnsReply()
    {
        var repo = CreateRepository();
        var originalId = "https://example.com/notes/original";
        var reply = new Note
        {
            Id = "https://example.com/notes/reply1",
            Type = new[] { "Note" },
            Content = new[] { "reply content" },
            InReplyTo = new IObjectOrLink[] { new Link { Href = new Uri(originalId) } }
        };

        await repo.SaveInboxActivityAsync("alice", reply.Id!, reply);

        var replies = await repo.GetRepliesAsync(originalId);
        Assert.Single(replies);
    }

    [Fact]
    public async Task GetLikedByActorCountAsync_AfterLikeActivity_ReturnsCount()
    {
        var repo = CreateRepository();
        var like = new Like
        {
            Id = "https://example.com/likes/1",
            Type = new[] { "Like" },
            Object = new IObjectOrLink[] { new Note { Id = "https://example.com/notes/1", Type = new[] { "Note" } } },
            Actor = new IObjectOrLink[] { new Link { Href = new Uri("https://example.com/users/alice") } }
        };

        await repo.SaveOutboxActivityAsync("alice", like.Id!, like);

        Assert.Equal(1, await repo.GetLikedByActorCountAsync("alice"));
    }

    [Fact]
    public async Task GetSharedByActorCountAsync_AfterAnnounceActivity_ReturnsCount()
    {
        var repo = CreateRepository();
        var announce = new Announce
        {
            Id = "https://example.com/announces/1",
            Type = new[] { "Announce" },
            Object = new IObjectOrLink[] { new Note { Id = "https://example.com/notes/1", Type = new[] { "Note" } } },
            Actor = new IObjectOrLink[] { new Link { Href = new Uri("https://example.com/users/alice") } }
        };

        await repo.SaveOutboxActivityAsync("alice", announce.Id!, announce);

        Assert.Equal(1, await repo.GetSharedByActorCountAsync("alice"));
    }

    [Fact]
    public async Task GetInboxActivitiesAsync_WithOffset_SkipsCorrectItems()
    {
        var repo = CreateRepository();
        for (var i = 1; i <= 5; i++)
        {
            await repo.SaveInboxActivityAsync("alice",
                $"https://example.com/activities/{i}",
                CreateActivity($"https://example.com/activities/{i}", $"https://example.com/notes/{i}"));
        }

        var page = await repo.GetInboxActivitiesAsync("alice", limit: 3, offset: 2);

        Assert.Equal(3, page.Count());
    }
}

public class InMemoryActivityRepositoryTests : InMemoryOnlyActivityRepositoryTests
{
    protected override IActivityRepository CreateRepository() => new InMemoryActivityRepository();
}

public class FileSystemActivityRepositoryTests : ActivityRepositoryTests, IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "broca-activity-tests", Guid.NewGuid().ToString());

    protected override IActivityRepository CreateRepository() =>
        new FileSystemActivityRepository(
            Options.Create(new FileSystemPersistenceOptions { DataPath = _tempDir }),
            NullLogger<FileSystemActivityRepository>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
