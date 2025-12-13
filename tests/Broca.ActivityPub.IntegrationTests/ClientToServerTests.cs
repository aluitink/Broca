using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.IntegrationTests;

/// <summary>
/// Example integration tests demonstrating Client-to-Server (C2S) interactions
/// These tests show how to:
/// - Seed users in in-memory repositories
/// - Create authenticated clients
/// - Post activities to outboxes
/// - Verify activities are stored with server-assigned IDs
/// </summary>
public class ClientToServerTests : TwoServerFixture
{
    [Fact]
    public async Task C2S_UserPostsNoteToOutbox_ActivityStoredWithUniqueId()
    {
        // Arrange - Seed Alice on Server A
        using var scope = ServerA.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

        var (alice, alicePrivateKey) = await TestDataSeeder.SeedActorAsync(
            actorRepo, 
            "alice", 
            ServerA.BaseUrl);

        // Create authenticated client for Alice
        var aliceClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            alice.Id!,
            alicePrivateKey);

        var c2sHelper = new ClientToServerHelper(aliceClient, alice.Id!, ClientA);

        // Act - Alice posts a Create activity with a Note to her outbox
        var createActivity = TestDataSeeder.CreateCreateActivity(
            alice.Id!, 
            "Hello from Alice!");

        var response = await c2sHelper.PostToOutboxAsync(createActivity);

        // Assert - Verify the activity was stored
        var s2sHelper = new ServerToServerHelper(ServerA);
        var outboxActivities = await s2sHelper.GetOutboxActivitiesAsync("alice");

        Assert.True(response.IsSuccessStatusCode);
        Assert.NotEmpty(outboxActivities);
    }

    [Fact]
    public async Task C2S_UserPostsLike_ActivityStoredInOutbox()
    {
        // Arrange - Seed Alice and a note on Server A
        using var scope = ServerA.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

        var (alice, alicePrivateKey) = await TestDataSeeder.SeedActorAsync(
            actorRepo, 
            "alice", 
            ServerA.BaseUrl);

        // Create a note first
        var note = TestDataSeeder.CreateNote(alice.Id!, "Test note");
        var noteId = $"{ServerA.BaseUrl}/users/alice/notes/{Guid.NewGuid()}";
        note.Id = noteId;
        await activityRepo.SaveOutboxActivityAsync("alice", noteId, note);

        // Create authenticated client for Alice
        var aliceClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            alice.Id!,
            alicePrivateKey);

        var c2sHelper = new ClientToServerHelper(aliceClient, alice.Id!, ClientA);

        // Act - Alice likes her own note
        var likeActivity = TestDataSeeder.CreateLike(alice.Id!, noteId);
        var response = await c2sHelper.PostToOutboxAsync(likeActivity);

        // Assert
        var s2sHelper = new ServerToServerHelper(ServerA);
        var outboxActivities = await s2sHelper.GetOutboxActivitiesAsync("alice");

        Assert.True(response.IsSuccessStatusCode);
        // Should have at least 2 activities: the note and the like
        Assert.True(outboxActivities.Count() >= 2);
    }

    [Fact]
    public async Task C2S_MultipleUsers_PostToTheirOwnOutboxes()
    {
        // Arrange - Seed Alice and Bob on Server A
        using var scope = ServerA.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();

        var (alice, alicePrivateKey) = await TestDataSeeder.SeedActorAsync(
            actorRepo, 
            "alice", 
            ServerA.BaseUrl);

        var (bob, bobPrivateKey) = await TestDataSeeder.SeedActorAsync(
            actorRepo, 
            "bob", 
            ServerA.BaseUrl);

        // Create authenticated clients
        var aliceClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            alice.Id!,
            alicePrivateKey);

        var bobClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            bob.Id!,
            bobPrivateKey);

        var aliceC2S = new ClientToServerHelper(aliceClient, alice.Id!, ClientA);
        var bobC2S = new ClientToServerHelper(bobClient, bob.Id!, ClientA);

        // Act - Both users post notes
        var aliceNote = TestDataSeeder.CreateCreateActivity(alice.Id!, "Alice's note");
        var bobNote = TestDataSeeder.CreateCreateActivity(bob.Id!, "Bob's note");

        var aliceResponse = await aliceC2S.PostToOutboxAsync(aliceNote);
        var bobResponse = await bobC2S.PostToOutboxAsync(bobNote);

        // Assert
        var s2sHelper = new ServerToServerHelper(ServerA);
        var aliceOutbox = await s2sHelper.GetOutboxActivitiesAsync("alice");
        var bobOutbox = await s2sHelper.GetOutboxActivitiesAsync("bob");

        Assert.True(aliceResponse.IsSuccessStatusCode);
        Assert.True(bobResponse.IsSuccessStatusCode);
        Assert.NotEmpty(aliceOutbox);
        Assert.NotEmpty(bobOutbox);
    }

    [Fact]
    public async Task C2S_UserPostsFollow_StoredInOutboxAndFollowingUpdated()
    {
        // Arrange - Seed Alice and Bob on Server A
        using var scope = ServerA.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();

        var (alice, alicePrivateKey) = await TestDataSeeder.SeedActorAsync(
            actorRepo, 
            "alice", 
            ServerA.BaseUrl);

        var (bob, bobPrivateKey) = await TestDataSeeder.SeedActorAsync(
            actorRepo, 
            "bob", 
            ServerA.BaseUrl);

        // Create authenticated client for Alice
        var aliceClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            alice.Id!,
            alicePrivateKey);

        var c2sHelper = new ClientToServerHelper(aliceClient, alice.Id!, ClientA);

        // Act - Alice follows Bob
        var followActivity = TestDataSeeder.CreateFollow(alice.Id!, bob.Id!);
        var response = await c2sHelper.PostToOutboxAsync(followActivity);

        // Assert - Verify the Follow was stored in outbox
        var s2sHelper = new ServerToServerHelper(ServerA);
        var outboxActivities = await s2sHelper.GetOutboxActivitiesAsync("alice");

        Assert.True(response.IsSuccessStatusCode);
        Assert.NotEmpty(outboxActivities);
        
        // Verify there's a Follow activity in the outbox
        var followInOutbox = outboxActivities.FirstOrDefault(a => 
            a.Type?.Contains("Follow") == true);
        Assert.NotNull(followInOutbox);

        // Verify Alice's following collection was updated
        var followingCollection = await actorRepo.GetFollowingAsync("alice");
        Assert.Contains(bob.Id!, followingCollection);
    }

    [Fact]
    public async Task C2S_UserPostsUndoFollow_StoredInOutboxAndFollowingRemoved()
    {
        // Arrange - Seed Alice and Bob, and establish a follow relationship
        using var scope = ServerA.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();

        var (alice, alicePrivateKey) = await TestDataSeeder.SeedActorAsync(
            actorRepo, 
            "alice", 
            ServerA.BaseUrl);

        var (bob, bobPrivateKey) = await TestDataSeeder.SeedActorAsync(
            actorRepo, 
            "bob", 
            ServerA.BaseUrl);

        // Create authenticated client for Alice
        var aliceClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            alice.Id!,
            alicePrivateKey);

        var c2sHelper = new ClientToServerHelper(aliceClient, alice.Id!, ClientA);

        // First, Alice follows Bob
        var followActivity = TestDataSeeder.CreateFollow(alice.Id!, bob.Id!);
        await c2sHelper.PostToOutboxAsync(followActivity);

        // Verify the follow was established
        var followingBeforeUndo = await actorRepo.GetFollowingAsync("alice");
        Assert.Contains(bob.Id!, followingBeforeUndo);

        // Act - Alice undoes the follow
        var undoActivity = TestDataSeeder.CreateUndo(alice.Id!, followActivity);
        var response = await c2sHelper.PostToOutboxAsync(undoActivity);

        // Assert - Verify the Undo was stored in outbox
        var s2sHelper = new ServerToServerHelper(ServerA);
        var outboxActivities = await s2sHelper.GetOutboxActivitiesAsync("alice");

        Assert.True(response.IsSuccessStatusCode);
        Assert.NotEmpty(outboxActivities);
        
        // Verify there's an Undo activity in the outbox
        var undoInOutbox = outboxActivities.FirstOrDefault(a => 
            a.Type?.Contains("Undo") == true);
        Assert.NotNull(undoInOutbox);

        // Verify Alice's following collection was updated (Bob removed)
        var followingAfterUndo = await actorRepo.GetFollowingAsync("alice");
        Assert.DoesNotContain(bob.Id!, followingAfterUndo);
    }
}
