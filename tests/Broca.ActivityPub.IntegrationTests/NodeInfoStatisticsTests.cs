using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.IntegrationTests.Infrastructure;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Broca.ActivityPub.IntegrationTests;

/// <summary>
/// Integration tests for NodeInfo statistics (M5)
/// Verifies that real usage stats are returned when repositories implement IActorStatistics and IActivityStatistics
/// </summary>
public class NodeInfoStatisticsTests : MultiServerTestFixture
{
    private BrocaTestServer Server => GetServer("MainServer");
    private HttpClient Client => GetClient("MainServer");

    protected override async Task SetupServersAsync()
    {
        await CreateServerAsync("MainServer", "https://test-instance.example", "test-instance.example");
    }

    [Fact]
    public async Task NodeInfo20_ReturnsRealUserCount_WhenActorsExist()
    {
        // Arrange - Seed multiple actors
        using (var scope = Server.Services.CreateScope())
        {
            var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "alice", Server.BaseUrl);
            await TestDataSeeder.SeedActorAsync(actorRepo, "bob", Server.BaseUrl);
            await TestDataSeeder.SeedActorAsync(actorRepo, "carol", Server.BaseUrl);
        }

        // Act - Fetch NodeInfo 2.0
        var response = await Client.GetAsync("/nodeinfo/2.0");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var nodeInfo = JsonDocument.Parse(json);

        var totalUsers = nodeInfo.RootElement.GetProperty("usage").GetProperty("users").GetProperty("total").GetInt32();
        Assert.Equal(3, totalUsers);
    }

    [Fact]
    public async Task NodeInfo20_ReturnsRealActiveUserStats_WhenUsersHaveRecentPosts()
    {
        // Arrange - Seed actors and create activities
        string alicePrivateKey, bobPrivateKey, carolPrivateKey;
        
        using (var scope = Server.Services.CreateScope())
        {
            var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
            var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
            
            var (alice, aliceKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "alice", Server.BaseUrl);
            var (bob, bobKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "bob", Server.BaseUrl);
            var (carol, carolKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "carol", Server.BaseUrl);
            
            alicePrivateKey = aliceKey;
            bobPrivateKey = bobKey;
            carolPrivateKey = carolKey;

            // Alice posts a recent note (active this month)
            var aliceNote = new Note
            {
                Type = new List<string> { "Note" },
                Id = $"{Server.BaseUrl}/users/alice/objects/note1",
                AttributedTo = new List<IObjectOrLink> { new Link { Href = new Uri(alice.Id!) } },
                Content = new List<string> { "Alice's recent post" },
                Published = DateTime.UtcNow.AddDays(-5)
            };
            var aliceCreate = new Create
            {
                Type = new List<string> { "Create" },
                Id = $"{Server.BaseUrl}/users/alice/activities/create1",
                Actor = new List<IObjectOrLink> { new Link { Href = new Uri(alice.Id!) } },
                Object = new List<IObjectOrLink> { aliceNote },
                Published = DateTime.UtcNow.AddDays(-5)
            };
            await activityRepo.SaveOutboxActivityAsync("alice", aliceCreate.Id!, aliceCreate);

            // Bob posts an old note (active half year but not this month)
            var bobNote = new Note
            {
                Type = new List<string> { "Note" },
                Id = $"{Server.BaseUrl}/users/bob/objects/note1",
                AttributedTo = new List<IObjectOrLink> { new Link { Href = new Uri(bob.Id!) } },
                Content = new List<string> { "Bob's old post" },
                Published = DateTime.UtcNow.AddDays(-60)
            };
            var bobCreate = new Create
            {
                Type = new List<string> { "Create" },
                Id = $"{Server.BaseUrl}/users/bob/activities/create1",
                Actor = new List<IObjectOrLink> { new Link { Href = new Uri(bob.Id!) } },
                Object = new List<IObjectOrLink> { bobNote },
                Published = DateTime.UtcNow.AddDays(-60)
            };
            await activityRepo.SaveOutboxActivityAsync("bob", bobCreate.Id!, bobCreate);

            // Carol has no posts (not active)
        }

        // Act - Fetch NodeInfo 2.0
        var response = await Client.GetAsync("/nodeinfo/2.0");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var nodeInfo = JsonDocument.Parse(json);

        var usage = nodeInfo.RootElement.GetProperty("usage");
        var users = usage.GetProperty("users");
        
        var totalUsers = users.GetProperty("total").GetInt32();
        var activeMonth = users.GetProperty("activeMonth").GetInt32();
        var activeHalfyear = users.GetProperty("activeHalfyear").GetInt32();
        var localPosts = usage.GetProperty("localPosts").GetInt32();

        Assert.Equal(3, totalUsers);
        Assert.Equal(1, activeMonth); // Only Alice
        Assert.Equal(2, activeHalfyear); // Alice and Bob
        Assert.Equal(2, localPosts); // Two Create activities total
    }

    [Fact]
    public async Task NodeInfo21_ReturnsRealStats_SameAsNodeInfo20()
    {
        // Arrange - Seed actors
        using (var scope = Server.Services.CreateScope())
        {
            var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "alice", Server.BaseUrl);
            await TestDataSeeder.SeedActorAsync(actorRepo, "bob", Server.BaseUrl);
        }

        // Act - Fetch both NodeInfo versions
        var response20 = await Client.GetAsync("/nodeinfo/2.0");
        var response21 = await Client.GetAsync("/nodeinfo/2.1");

        // Assert
        response20.EnsureSuccessStatusCode();
        response21.EnsureSuccessStatusCode();

        var json20 = await response20.Content.ReadAsStringAsync();
        var json21 = await response21.Content.ReadAsStringAsync();

        var nodeInfo20 = JsonDocument.Parse(json20);
        var nodeInfo21 = JsonDocument.Parse(json21);

        var usage20 = nodeInfo20.RootElement.GetProperty("usage");
        var usage21 = nodeInfo21.RootElement.GetProperty("usage");

        // Both should report the same stats
        Assert.Equal(
            usage20.GetProperty("users").GetProperty("total").GetInt32(),
            usage21.GetProperty("users").GetProperty("total").GetInt32()
        );
    }

    [Fact]
    public async Task NodeInfo20_ReturnsMinimalStats_WhenNoActorsExist()
    {
        // Arrange - Empty instance (no actors)
        // Don't seed any data

        // Act - Fetch NodeInfo 2.0
        var response = await Client.GetAsync("/nodeinfo/2.0");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var nodeInfo = JsonDocument.Parse(json);

        var usage = nodeInfo.RootElement.GetProperty("usage");
        var users = usage.GetProperty("users");
        
        var totalUsers = users.GetProperty("total").GetInt32();
        var localPosts = usage.GetProperty("localPosts").GetInt32();

        Assert.Equal(0, totalUsers);
        Assert.Equal(0, localPosts);
    }

    [Fact]
    public async Task XNodeInfo2_ReturnsRealStats()
    {
        // Arrange - Seed actors
        using (var scope = Server.Services.CreateScope())
        {
            var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "alice", Server.BaseUrl);
        }

        // Act - Fetch x-nodeinfo2
        var response = await Client.GetAsync("/.well-known/x-nodeinfo2");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var nodeInfo = JsonDocument.Parse(json);

        var totalUsers = nodeInfo.RootElement.GetProperty("usage").GetProperty("users").GetProperty("total").GetInt32();
        Assert.Equal(1, totalUsers);
    }
}
