using System.Net;
using System.Text;
using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.IntegrationTests.Infrastructure;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.IntegrationTests;

public class OutboxAuthenticationTests : IAsyncLifetime
{
    private BrocaTestServer _server = null!;
    private Actor _alice = null!;
    private string _alicePrivateKey = null!;
    private Actor _bob = null!;
    private string _bobPrivateKey = null!;

    public async Task InitializeAsync()
    {
        _server = new BrocaTestServer(
            "https://test-server.local",
            "test-server.local",
            "TestServer");

        await _server.InitializeAsync();

        using var scope = _server.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();

        (_alice, _alicePrivateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "alice", _server.BaseUrl);
        (_bob, _bobPrivateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "bob", _server.BaseUrl);
    }

    public async Task DisposeAsync()
    {
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task OutboxPost_WithoutSignature_Returns401()
    {
        var activity = TestDataSeeder.CreateCreateActivity(_alice.Id!, "Hello!");
        var json = JsonSerializer.Serialize<IObjectOrLink>(activity, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var httpClient = _server.CreateClient();
        var content = new StringContent(json, Encoding.UTF8, "application/activity+json");
        var response = await httpClient.PostAsync("/users/alice/outbox", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OutboxPost_SignedByDifferentActor_Returns403()
    {
        var activity = TestDataSeeder.CreateCreateActivity(_alice.Id!, "Hello!");

        var bobClient = TestClientFactory.CreateAuthenticatedClient(
            () => _server.CreateClient(),
            _bob.Id!,
            _bobPrivateKey);

        var response = await bobClient.PostAsync<Activity>(
            new Uri($"{_server.BaseUrl}/users/alice/outbox"),
            activity);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OutboxPost_SignedByCorrectActor_Returns201()
    {
        var activity = TestDataSeeder.CreateCreateActivity(_alice.Id!, "Hello from Alice!");

        var aliceClient = TestClientFactory.CreateAuthenticatedClient(
            () => _server.CreateClient(),
            _alice.Id!,
            _alicePrivateKey);

        var response = await aliceClient.PostAsync<Activity>(
            new Uri($"{_server.BaseUrl}/users/alice/outbox"),
            activity);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
