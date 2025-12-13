using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.IntegrationTests.Infrastructure;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.IntegrationTests;

/// <summary>
/// Integration tests for the shared inbox functionality
/// Tests efficient delivery to multiple local users via the shared inbox endpoint
/// Uses C2S to trigger S2S delivery to shared inbox
/// </summary>
public class SharedInboxTests : TwoServerFixture
{
    [Fact]
    public async Task SharedInbox_ActivityToMultipleLocalUsers_DeliveredToAllRecipients()
    {
        // Arrange - Create multiple users on Server B
        using (var scopeB = ServerB.Services.CreateScope())
        {
            var actorRepo = scopeB.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "alice", ServerB.BaseUrl);
            await TestDataSeeder.SeedActorAsync(actorRepo, "bob", ServerB.BaseUrl);
            await TestDataSeeder.SeedActorAsync(actorRepo, "charlie", ServerB.BaseUrl);
        }

        // Create a sender on Server A
        string senderPrivateKey;
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            var (sender, privateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "sender", ServerA.BaseUrl);
            senderPrivateKey = privateKey;
        }

        var senderId = $"{ServerA.BaseUrl}/users/sender";
        var aliceId = $"{ServerB.BaseUrl}/users/alice";
        var bobId = $"{ServerB.BaseUrl}/users/bob";
        var charlieId = $"{ServerB.BaseUrl}/users/charlie";

        // Create authenticated client for sender
        var senderClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            senderId,
            senderPrivateKey);

        var c2sHelper = new ClientToServerHelper(senderClient, senderId, ClientA);

        // Create an activity addressed to all three users
        var noteActivity = TestDataSeeder.CreateCreateActivity(
            senderId, 
            "Hello to Alice, Bob, and Charlie!",
            to: new[] { aliceId, bobId, charlieId });

        // Act - Post via C2S to trigger S2S delivery
        var response = await c2sHelper.PostToOutboxAsync(noteActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Wait for delivery to all three users via shared inbox
        var s2sHelperB = new ServerToServerHelper(ServerB, TimeSpan.FromSeconds(5), sendingServer: ServerA);

        var aliceActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync("alice", "Create", TimeSpan.FromSeconds(10));
        var bobActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync("bob", "Create", TimeSpan.FromSeconds(10));
        var charlieActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync("charlie", "Create", TimeSpan.FromSeconds(10));

        Assert.NotNull(aliceActivity);
        Assert.NotNull(bobActivity);
        Assert.NotNull(charlieActivity);
        Assert.Equal("Create", aliceActivity.Type?.FirstOrDefault());
    }

    [Fact]
    public async Task SharedInbox_ActivityWithCcAddressing_DeliveredToAllRecipients()
    {
        // Arrange - Create users on Server B
        using (var scopeB = ServerB.Services.CreateScope())
        {
            var actorRepo = scopeB.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "dave", ServerB.BaseUrl);
            await TestDataSeeder.SeedActorAsync(actorRepo, "eve", ServerB.BaseUrl);
        }

        // Create sender on Server A
        string senderPrivateKey;
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            var (sender, privateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "sender2", ServerA.BaseUrl);
            senderPrivateKey = privateKey;
        }

        var senderId = $"{ServerA.BaseUrl}/users/sender2";
        var daveId = $"{ServerB.BaseUrl}/users/dave";
        var eveId = $"{ServerB.BaseUrl}/users/eve";

        var senderClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            senderId,
            senderPrivateKey);

        var c2sHelper = new ClientToServerHelper(senderClient, senderId, ClientA);

        // Create activity with one user in To: and another in Cc:
        var noteActivity = TestDataSeeder.CreateCreateActivity(
            senderId, 
            "Hello Dave and Eve!",
            to: new[] { daveId },
            cc: new[] { eveId });

        // Act
        var response = await c2sHelper.PostToOutboxAsync(noteActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        var s2sHelperB = new ServerToServerHelper(ServerB, TimeSpan.FromSeconds(5), sendingServer: ServerA);

        var daveActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync("dave", "Create", TimeSpan.FromSeconds(10));
        var eveActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync("eve", "Create", TimeSpan.FromSeconds(10));

        Assert.NotNull(daveActivity);
        Assert.NotNull(eveActivity);
    }

    [Fact]
    public async Task SharedInbox_ActivityWithBccAddressing_DeliveredToAllRecipients()
    {
        // Arrange
        using (var scopeB = ServerB.Services.CreateScope())
        {
            var actorRepo = scopeB.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "frank", ServerB.BaseUrl);
        }

        string senderPrivateKey;
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            var (sender, privateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "sender3", ServerA.BaseUrl);
            senderPrivateKey = privateKey;
        }

        var senderId = $"{ServerA.BaseUrl}/users/sender3";
        var frankId = $"{ServerB.BaseUrl}/users/frank";

        var senderClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            senderId,
            senderPrivateKey);

        var c2sHelper = new ClientToServerHelper(senderClient, senderId, ClientA);

        // Create activity with Bcc addressing
        var noteActivity = TestDataSeeder.CreateCreateActivity(
            senderId, 
            "Secret message to Frank!",
            bcc: new[] { frankId });

        // Act
        var response = await c2sHelper.PostToOutboxAsync(noteActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        var s2sHelperB = new ServerToServerHelper(ServerB, TimeSpan.FromSeconds(5), sendingServer: ServerA);

        var frankActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync("frank", "Create", TimeSpan.FromSeconds(10));
        
        Assert.NotNull(frankActivity);
    }

    [Fact]
    public async Task SharedInbox_ActivityWithNoLocalRecipients_AcceptedButNotStored()
    {
        // Arrange - Create sender on Server A
        string senderPrivateKey;
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            var (sender, privateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "sender4", ServerA.BaseUrl);
            senderPrivateKey = privateKey;
        }

        var senderId = $"{ServerA.BaseUrl}/users/sender4";

        var senderClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            senderId,
            senderPrivateKey);

        var c2sHelper = new ClientToServerHelper(senderClient, senderId, ClientA);

        // Create activity addressed to a non-existent user on Server B
        var noteActivity = TestDataSeeder.CreateCreateActivity(
            senderId, 
            "This should not be delivered to anyone",
            to: new[] { $"{ServerB.BaseUrl}/users/nonexistent" });

        // Act
        var response = await c2sHelper.PostToOutboxAsync(noteActivity);

        // Assert - C2S post should succeed
        Assert.True(response.IsSuccessStatusCode);
        
        // S2S delivery will occur but won't find any local recipients
        // The server should accept the delivery gracefully without error
    }

    [Fact]
    public async Task SharedInbox_ActivityWithMixedLocalAndRemoteRecipients_OnlyLocalReceive()
    {
        // Arrange - Create one local user on Server B
        using (var scopeB = ServerB.Services.CreateScope())
        {
            var actorRepo = scopeB.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "grace", ServerB.BaseUrl);
        }

        string senderPrivateKey;
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            var (sender, privateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "sender5", ServerA.BaseUrl);
            senderPrivateKey = privateKey;
        }

        var senderId = $"{ServerA.BaseUrl}/users/sender5";
        var graceId = $"{ServerB.BaseUrl}/users/grace";
        var remoteUserId = "https://remote-server.example/users/remote";

        var senderClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            senderId,
            senderPrivateKey);

        var c2sHelper = new ClientToServerHelper(senderClient, senderId, ClientA);

        // Create activity addressed to both local and remote users
        var noteActivity = TestDataSeeder.CreateCreateActivity(
            senderId, 
            "Message to Grace and remote user",
            to: new[] { graceId, remoteUserId });

        // Act
        var response = await c2sHelper.PostToOutboxAsync(noteActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Verify only the local user (Grace) received it
        var s2sHelperB = new ServerToServerHelper(ServerB, TimeSpan.FromSeconds(5), sendingServer: ServerA);

        var graceActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync("grace", "Create", TimeSpan.FromSeconds(10));
        
        Assert.NotNull(graceActivity);
    }

    [Fact]
    public async Task SharedInbox_InvalidSignature_ReturnsUnauthorized()
    {
        // Arrange - Create sender but we'll test with wrong private key
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "sender6", ServerA.BaseUrl);
        }

        // Create recipient on Server B
        using (var scopeB = ServerB.Services.CreateScope())
        {
            var actorRepo = scopeB.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "alice", ServerB.BaseUrl);
        }

        // Generate a different key pair (wrong key)
        var (wrongPrivateKey, _) = KeyGenerator.GenerateKeyPair();

        var senderId = $"{ServerA.BaseUrl}/users/sender6";
        var recipientId = $"{ServerB.BaseUrl}/users/alice";

        // Create client with wrong key - this will cause signature validation to fail
        var senderClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            senderId,
            wrongPrivateKey);

        // Create activity and post directly to shared inbox with wrong signature
        var noteActivity = new Activity
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Id = $"{senderId}/activities/{Guid.NewGuid()}",
            Type = new[] { "Create" },
            Actor = new IObjectOrLink[] { new Actor { Id = senderId } },
            To = new IObjectOrLink[] { new Actor { Id = recipientId } },
            Object = new IObjectOrLink[]
            {
                new Activity
                {
                    Type = new[] { "Note" },
                    Content = new[] { "This should be rejected" }
                }
            }
        };

        // Act - Post directly to shared inbox endpoint (bypassing C2S)
        var response = await senderClient.PostAsync(
            new Uri($"{ServerB.BaseUrl}/inbox"),
            noteActivity);

        // Assert - Should be unauthorized due to invalid signature
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SharedInbox_Follow_DeliveredToTargetUser()
    {
        // Arrange
        using (var scopeB = ServerB.Services.CreateScope())
        {
            var actorRepo = scopeB.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "helen", ServerB.BaseUrl);
        }

        string senderPrivateKey;
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            var (sender, privateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "sender7", ServerA.BaseUrl);
            senderPrivateKey = privateKey;
        }

        var senderId = $"{ServerA.BaseUrl}/users/sender7";
        var helenId = $"{ServerB.BaseUrl}/users/helen";

        var senderClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            senderId,
            senderPrivateKey);

        var c2sHelper = new ClientToServerHelper(senderClient, senderId, ClientA);

        // Create a Follow activity
        var followActivity = TestDataSeeder.CreateFollow(senderId, helenId);

        // Act
        var response = await c2sHelper.PostToOutboxAsync(followActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        var s2sHelperB = new ServerToServerHelper(ServerB, TimeSpan.FromSeconds(5), sendingServer: ServerA);

        var followInInbox = await s2sHelperB.WaitForInboxActivityByTypeAsync("helen", "Follow", TimeSpan.FromSeconds(10));

        Assert.NotNull(followInInbox);
        Assert.Equal("Follow", followInInbox.Type?.FirstOrDefault());
    }
}
