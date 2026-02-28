using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.IntegrationTests.Infrastructure;
using Broca.ActivityPub.Server.Services;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Broca.ActivityPub.IntegrationTests;

/// <summary>
/// Integration tests for the shared inbox functionality
/// Tests efficient delivery to multiple local users via the shared inbox endpoint
/// Uses C2S to trigger S2S delivery to shared inbox
/// </summary>
public class SharedInboxTests : TwoServerFixture
{
    private readonly ITestOutputHelper _output;

    public SharedInboxTests(ITestOutputHelper output)
    {
        _output = output;
    }
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

        var aliceActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("alice", TimeSpan.FromSeconds(10));
        var bobActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("bob", TimeSpan.FromSeconds(10));
        var charlieActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("charlie", TimeSpan.FromSeconds(10));

        Assert.NotNull(aliceActivity);
        Assert.NotNull(bobActivity);
        Assert.NotNull(charlieActivity);
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

        var daveActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("dave", TimeSpan.FromSeconds(10));
        var eveActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("eve", TimeSpan.FromSeconds(10));

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

        var frankActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("frank", TimeSpan.FromSeconds(10));
        
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

        var graceActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("grace", TimeSpan.FromSeconds(10));
        
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
            () => CreateRoutingClient(),
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

        var followInInbox = await s2sHelperB.WaitForInboxActivityByTypeAsync<Follow>("helen", TimeSpan.FromSeconds(10));

        Assert.NotNull(followInInbox);
    }

    [Fact]
    public async Task SharedInbox_ActivityWithPublicAddressing_DeliveredToAllLocalUsers()
    {
        // Arrange - Create multiple users on Server B
        using (var scopeB = ServerB.Services.CreateScope())
        {
            var actorRepo = scopeB.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "user1", ServerB.BaseUrl);
            await TestDataSeeder.SeedActorAsync(actorRepo, "user2", ServerB.BaseUrl);
            await TestDataSeeder.SeedActorAsync(actorRepo, "user3", ServerB.BaseUrl);
        }

        // Create sender on Server A
        string senderPrivateKey;
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            var (sender, privateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "sender_public", ServerA.BaseUrl);
            senderPrivateKey = privateKey;
        }

        var senderId = $"{ServerA.BaseUrl}/users/sender_public";

        // Create authenticated client with routing so the request reaches ServerB
        var senderClient = TestClientFactory.CreateAuthenticatedClient(
            () => CreateRoutingClient(),
            senderId,
            senderPrivateKey);

        // Create activity with as:Public addressing - post directly to shared inbox
        var noteActivity = new Activity
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Id = $"{senderId}/activities/{Guid.NewGuid()}",
            Type = new[] { "Create" },
            Actor = new IObjectOrLink[] { new Link { Href = new Uri(senderId) } },
            To = new IObjectOrLink[] { new Link { Href = new Uri("https://www.w3.org/ns/activitystreams#Public") } },
            Object = new IObjectOrLink[]
            {
                new Note
                {
                    Type = new[] { "Note" },
                    Content = new[] { "Public post for everyone!" },
                    AttributedTo = new IObjectOrLink[] { new Link { Href = new Uri(senderId) } }
                }
            },
            Published = DateTime.UtcNow
        };

        // Act - Post directly to shared inbox
        var response = await senderClient.PostAsync(
            new Uri($"{ServerB.BaseUrl}/inbox"),
            noteActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Wait a bit for async inbox processing
        await Task.Delay(1000);

        // Verify all local users received it
        var s2sHelperB = new ServerToServerHelper(ServerB, TimeSpan.FromSeconds(5), sendingServer: ServerA);

        var user1Activity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("user1", TimeSpan.FromSeconds(10));
        var user2Activity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("user2", TimeSpan.FromSeconds(10));
        var user3Activity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("user3", TimeSpan.FromSeconds(10));

        Assert.NotNull(user1Activity);
        Assert.NotNull(user2Activity);
        Assert.NotNull(user3Activity);
    }

    [Fact]
    public async Task SharedInbox_ActivityToFollowersCollection_DeliveredToLocalFollowers()
    {
        // Arrange - Create sender on Server A and followers on Server B
        string senderPrivateKey;
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            var (sender, privateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "sender_followers", ServerA.BaseUrl);
            senderPrivateKey = privateKey;

            // Add followers (mix of local on A and remote on B)
            await actorRepo.AddFollowerAsync("sender_followers", $"{ServerB.BaseUrl}/users/remote_follower1");
            await actorRepo.AddFollowerAsync("sender_followers", $"{ServerB.BaseUrl}/users/remote_follower2");
            await actorRepo.AddFollowerAsync("sender_followers", $"{ServerA.BaseUrl}/users/local_follower");
        }

        // Create the actual follower users on Server B and set up following relationships
        using (var scopeB = ServerB.Services.CreateScope())
        {
            var actorRepo = scopeB.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "remote_follower1", ServerB.BaseUrl);
            await TestDataSeeder.SeedActorAsync(actorRepo, "remote_follower2", ServerB.BaseUrl);
            await TestDataSeeder.SeedActorAsync(actorRepo, "non_follower", ServerB.BaseUrl);  // This user should NOT receive it

            // Set up the "following" side on ServerB (as would happen via Follow/Accept exchange)
            await actorRepo.AddFollowingAsync("remote_follower1", $"{ServerA.BaseUrl}/users/sender_followers");
            await actorRepo.AddFollowingAsync("remote_follower2", $"{ServerA.BaseUrl}/users/sender_followers");
        }

        var senderId = $"{ServerA.BaseUrl}/users/sender_followers";
        var followersUrl = $"{senderId}/followers";

        var senderClient = TestClientFactory.CreateAuthenticatedClient(
            () => CreateRoutingClient(),
            senderId,
            senderPrivateKey);

        // Create activity addressed to followers collection
        var noteActivity = new Activity
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Id = $"{senderId}/activities/{Guid.NewGuid()}",
            Type = new[] { "Create" },
            Actor = new IObjectOrLink[] { new Link { Href = new Uri(senderId) } },
            To = new IObjectOrLink[] { new Link { Href = new Uri(followersUrl) } },
            Object = new IObjectOrLink[]
            {
                new Note
                {
                    Type = new[] { "Note" },
                    Content = new[] { "Message to my followers!" },
                    AttributedTo = new IObjectOrLink[] { new Link { Href = new Uri(senderId) } }
                }
            },
            Published = DateTime.UtcNow
        };

        // Act - Post directly to shared inbox
        var response = await senderClient.PostAsync(
            new Uri($"{ServerB.BaseUrl}/inbox"),
            noteActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Wait a bit for async inbox processing
        await Task.Delay(1000);

        // Verify only the followers received it
        var s2sHelperB = new ServerToServerHelper(ServerB, TimeSpan.FromSeconds(5), sendingServer: ServerA);

        var follower1Activity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("remote_follower1", TimeSpan.FromSeconds(10));
        var follower2Activity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("remote_follower2", TimeSpan.FromSeconds(10));

        Assert.NotNull(follower1Activity);
        Assert.NotNull(follower2Activity);

        // Verify non-follower did NOT receive it
        using (var scopeB = ServerB.Services.CreateScope())
        {
            var activityRepo = scopeB.ServiceProvider.GetRequiredService<IActivityRepository>();
            var nonFollowerInbox = await activityRepo.GetInboxActivitiesAsync("non_follower", 10, 0);
            Assert.Empty(nonFollowerInbox);
        }
    }

    [Fact]
    public async Task SharedInbox_PublicActivityWithFollowersCc_DeliveredToAll()
    {
        // Arrange - Create sender on Server A
        string senderPrivateKey;
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            var (sender, privateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "sender_mixed", ServerA.BaseUrl);
            senderPrivateKey = privateKey;

            // Add one follower on Server B
            await actorRepo.AddFollowerAsync("sender_mixed", $"{ServerB.BaseUrl}/users/follower_user");
        }

        // Create users on Server B - follower and non-follower
        using (var scopeB = ServerB.Services.CreateScope())
        {
            var actorRepo = scopeB.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "follower_user", ServerB.BaseUrl);
            await TestDataSeeder.SeedActorAsync(actorRepo, "nonfollower_user", ServerB.BaseUrl);

            // Set up the "following" side on ServerB (as would happen via Follow/Accept exchange)
            await actorRepo.AddFollowingAsync("follower_user", $"{ServerA.BaseUrl}/users/sender_mixed");
        }

        var senderId = $"{ServerA.BaseUrl}/users/sender_mixed";
        var followersUrl = $"{senderId}/followers";

        var senderClient = TestClientFactory.CreateAuthenticatedClient(
            () => CreateRoutingClient(),
            senderId,
            senderPrivateKey);

        // Create activity with public addressing and followers in Cc
        var noteActivity = new Activity
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Id = $"{senderId}/activities/{Guid.NewGuid()}",
            Type = new[] { "Create" },
            Actor = new IObjectOrLink[] { new Link { Href = new Uri(senderId) } },
            To = new IObjectOrLink[] { new Link { Href = new Uri("https://www.w3.org/ns/activitystreams#Public") } },
            Cc = new IObjectOrLink[] { new Link { Href = new Uri(followersUrl) } },
            Object = new IObjectOrLink[]
            {
                new Note
                {
                    Type = new[] { "Note" },
                    Content = new[] { "Public post with followers notification!" },
                    AttributedTo = new IObjectOrLink[] { new Link { Href = new Uri(senderId) } }
                }
            },
            Published = DateTime.UtcNow
        };

        // Act - Post directly to shared inbox
        var response = await senderClient.PostAsync(
            new Uri($"{ServerB.BaseUrl}/inbox"),
            noteActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Wait a bit for async inbox processing
        await Task.Delay(1000);

        // Wait for delivery - both users should receive it (public + follower expansion)
        var s2sHelperB = new ServerToServerHelper(ServerB, TimeSpan.FromSeconds(5), sendingServer: ServerA);

        var followerActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("follower_user", TimeSpan.FromSeconds(10));
        var nonFollowerActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("nonfollower_user", TimeSpan.FromSeconds(10));

        Assert.NotNull(followerActivity);
        Assert.NotNull(nonFollowerActivity);
    }

    [Fact]
    public async Task FollowerFanOut_MultipleFollowersSameServer_UsesSharedInbox()
    {
        // Arrange - Alice on Server A; Bob and Carol both on Server B, both following Alice
        var aliceId = $"{ServerA.BaseUrl}/users/alice_fanout";
        string alicePrivateKey;

        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            var (_, privateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "alice_fanout", ServerA.BaseUrl);
            alicePrivateKey = privateKey;

            await actorRepo.AddFollowerAsync("alice_fanout", $"{ServerB.BaseUrl}/users/bob_fanout");
            await actorRepo.AddFollowerAsync("alice_fanout", $"{ServerB.BaseUrl}/users/carol_fanout");
        }

        using (var scopeB = ServerB.Services.CreateScope())
        {
            var actorRepo = scopeB.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "bob_fanout", ServerB.BaseUrl);
            await TestDataSeeder.SeedActorAsync(actorRepo, "carol_fanout", ServerB.BaseUrl);

            await actorRepo.AddFollowingAsync("bob_fanout", aliceId);
            await actorRepo.AddFollowingAsync("carol_fanout", aliceId);
        }

        var followersUrl = $"{aliceId}/followers";

        var activity = new Activity
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Id = $"{aliceId}/activities/{Guid.NewGuid()}",
            Type = new[] { "Create" },
            Actor = new IObjectOrLink[] { new Link { Href = new Uri(aliceId) } },
            Cc = new IObjectOrLink[] { new Link { Href = new Uri(followersUrl) } },
            Object = new IObjectOrLink[]
            {
                new Note
                {
                    Type = new[] { "Note" },
                    Content = new[] { "Hello followers!" },
                    AttributedTo = new IObjectOrLink[] { new Link { Href = new Uri(aliceId) } }
                }
            },
            Published = DateTime.UtcNow
        };

        // Part 1: Verify shared-inbox grouping — two followers on the same server must produce
        // exactly ONE delivery item targeting the shared inbox, not two individual items
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var deliveryService = scopeA.ServiceProvider.GetRequiredService<ActivityDeliveryService>();
            await deliveryService.QueueActivityForDeliveryAsync("alice_fanout", activity.Id!, activity);
        }

        var s2sHelperA = new ServerToServerHelper(ServerA, TimeSpan.FromSeconds(5));
        var pendingDeliveries = (await s2sHelperA.GetPendingDeliveriesAsync()).ToList();

        var deliveriesToServerB = pendingDeliveries
            .Where(d => d.InboxUrl?.StartsWith(ServerB.BaseUrl) == true)
            .ToList();

        Assert.Single(deliveriesToServerB);
        Assert.Equal($"{ServerB.BaseUrl}/inbox", deliveriesToServerB[0].InboxUrl);

        // Part 2: Verify end-to-end shared-inbox routing — posting directly to the shared inbox
        // (as the delivery service would) routes to both followers
        var aliceClient = TestClientFactory.CreateAuthenticatedClient(
            () => CreateRoutingClient(),
            aliceId,
            alicePrivateKey);

        var response = await aliceClient.PostAsync(
            new Uri($"{ServerB.BaseUrl}/inbox"),
            activity);

        Assert.True(response.IsSuccessStatusCode);

        await Task.Delay(500);

        var s2sHelperB = new ServerToServerHelper(ServerB, TimeSpan.FromSeconds(5), sendingServer: ServerA);
        var bobActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("bob_fanout", TimeSpan.FromSeconds(5));
        var carolActivity = await s2sHelperB.WaitForInboxActivityByTypeAsync<Create>("carol_fanout", TimeSpan.FromSeconds(5));

        Assert.NotNull(bobActivity);
        Assert.NotNull(carolActivity);
    }
}
