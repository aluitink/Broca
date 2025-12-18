using Broca.ActivityPub.Client.Extensions;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.IntegrationTests.Infrastructure;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Broca.ActivityPub.IntegrationTests;

/// <summary>
/// Integration tests for client authentication mechanisms
/// Tests API key-based and private key-based authentication
/// </summary>
public class ClientAuthenticationTests : IAsyncLifetime
{
    private BrocaTestServer _server = null!;
    private const string ApiKey = "test-api-key-12345";

    public async Task InitializeAsync()
    {
        var config = new Dictionary<string, string?>
        {
            ["ActivityPub:AdminApiToken"] = ApiKey
        };

        _server = new BrocaTestServer(
            "https://test-server.local",
            "test-server.local",
            "TestServer",
            additionalConfiguration: config);

        await _server.InitializeAsync();

        // Create a test user
        using var scope = _server.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        
        var (alice, _) = await TestDataSeeder.SeedActorAsync(
            actorRepo,
            "alice",
            _server.BaseUrl);
    }

    public async Task DisposeAsync()
    {
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task ClientWithApiKey_InitializeAsync_FetchesPrivateKeyAndCanAuthenticate()
    {
        // Arrange - Create a client configured with API key only
        var actorId = $"{_server.BaseUrl}/users/alice";
        
        var services = new ServiceCollection();
        services.AddActivityPubClientWithApiKey(actorId, ApiKey);
        services.AddSingleton<IHttpClientFactory>(new TestHttpClientFactory(() => _server.CreateClient()));
        
        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IActivityPubClient>();

        // Verify client requires initialization
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ActivityPubClientOptions>>().Value;
        Assert.True(options.RequiresInitialization);
        Assert.True(options.IsAuthenticated); // Client is authenticated mode because it has ActorId + ApiKey
        Assert.Null(options.PrivateKeyPem); // But doesn't have private key yet

        // Act - Initialize the client (fetch private key using API key)
        await client.InitializeAsync();

        // Assert - Client should now have private key
        Assert.False(options.RequiresInitialization);
        Assert.True(options.IsAuthenticated);
        Assert.NotNull(options.PrivateKeyPem);
        Assert.NotNull(options.PublicKeyId);
        Assert.Equal(actorId, options.ActorId);

        // Verify client can make authenticated requests
        var actor = await client.GetSelfAsync();
        Assert.NotNull(actor);
        Assert.Equal("alice", actor.PreferredUsername);
    }

    [Fact]
    public async Task ClientWithPrivateKey_DoesNotRequireInitialization()
    {
        // Arrange - Create a user and get their private key
        using var scope = _server.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        
        var (bob, bobPrivateKey) = await TestDataSeeder.SeedActorAsync(
            actorRepo,
            "bob",
            _server.BaseUrl);

        var actorId = bob.Id!;
        var publicKeyId = $"{actorId}#main-key";

        // Act - Create client with private key directly
        var services = new ServiceCollection();
        services.AddActivityPubClientAuthenticated(actorId, bobPrivateKey, publicKeyId);
        services.AddSingleton<IHttpClientFactory>(new TestHttpClientFactory(() => _server.CreateClient()));
        
        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IActivityPubClient>();

        // Assert - Client should not require initialization
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ActivityPubClientOptions>>().Value;
        Assert.False(options.RequiresInitialization);
        Assert.True(options.IsAuthenticated);
        Assert.Equal(bobPrivateKey, options.PrivateKeyPem);

        // Verify client can make authenticated requests without calling InitializeAsync
        var actor = await client.GetSelfAsync();
        Assert.NotNull(actor);
        Assert.Equal("bob", actor.PreferredUsername);
    }

    [Fact]
    public async Task ClientWithApiKey_CanPostToOutbox_AfterInitialization()
    {
        // Arrange - Create client with API key
        var actorId = $"{_server.BaseUrl}/users/alice";
        
        var services = new ServiceCollection();
        services.AddActivityPubClientWithApiKey(actorId, ApiKey);
        services.AddSingleton<IHttpClientFactory>(new TestHttpClientFactory(() => _server.CreateClient()));
        
        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IActivityPubClient>();

        // Initialize the client
        await client.InitializeAsync();

        // Act - Create and post an activity to the outbox
        var note = new Note
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Content = new[] { "Hello from API key authenticated client!" },
            AttributedTo = new[] { new Link { Href = new Uri(actorId) } }
        };

        var createActivity = new Create
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Actor = new[] { new Link { Href = new Uri(actorId) } },
            Object = new[] { note }
        };

        var response = await client.PostToOutboxAsync(createActivity);

        // Assert - Activity should be posted successfully
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Verify activity is in outbox
        using var scope = _server.Services.CreateScope();
        var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
        var outboxActivities = await activityRepo.GetOutboxActivitiesAsync("alice", 10, 0);
        
        Assert.NotEmpty(outboxActivities);
    }

    [Fact]
    public async Task ClientWithInvalidApiKey_InitializeAsync_ThrowsException()
    {
        // Arrange - Create client with invalid API key
        var actorId = $"{_server.BaseUrl}/users/alice";
        
        var services = new ServiceCollection();
        services.AddActivityPubClientWithApiKey(actorId, "invalid-key");
        services.AddSingleton<IHttpClientFactory>(new TestHttpClientFactory(() => _server.CreateClient()));
        
        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IActivityPubClient>();

        // Act & Assert - InitializeAsync should fail
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.InitializeAsync());

        Assert.Contains("Failed to initialize client", exception.Message);
    }

    [Fact]
    public Task ClientWithNoApiKeyOrPrivateKey_IsNotAuthenticated()
    {
        // Arrange - Create anonymous client
        var services = new ServiceCollection();
        services.AddActivityPubClient(); // Anonymous mode
        services.AddSingleton<IHttpClientFactory>(new TestHttpClientFactory(() => _server.CreateClient()));
        
        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IActivityPubClient>();

        // Assert
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ActivityPubClientOptions>>().Value;
        Assert.False(options.IsAuthenticated);
        Assert.False(options.RequiresInitialization);
        Assert.Null(options.ActorId);
        Assert.Null(options.PrivateKeyPem);
        Assert.Null(options.ApiKey);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Simple HttpClientFactory implementation for testing
    /// </summary>
    private class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpClient> _factory;

        public TestHttpClientFactory(Func<HttpClient> factory)
        {
            _factory = factory;
        }

        public HttpClient CreateClient(string name)
        {
            return _factory();
        }
    }
}
