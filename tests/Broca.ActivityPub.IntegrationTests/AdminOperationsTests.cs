using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.IntegrationTests.Infrastructure;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Broca.ActivityPub.IntegrationTests;

/// <summary>
/// Integration tests for admin back-channel user management
/// Tests creating, updating, and deleting users via ActivityPub protocol
/// </summary>
public class AdminOperationsTests : IAsyncLifetime
{
    private BrocaTestServer _server = null!;
    private string _systemActorId = null!;
    private string _systemPrivateKey = null!;
    private IActivityPubClient _authenticatedClient = null!;
    private IActivityBuilderFactory _activityBuilderFactory = null!;

    public async Task InitializeAsync()
    {
        _server = new BrocaTestServer(
            "https://test-server.local",
            "test-server.local",
            "TestServer");

        await _server.InitializeAsync();

        // Get system actor credentials
        using var scope = _server.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        _activityBuilderFactory = scope.ServiceProvider.GetRequiredService<IActivityBuilderFactory>();
        var systemActor = await actorRepo.GetActorByUsernameAsync("sys");
        
        Assert.NotNull(systemActor);
        _systemActorId = systemActor.Id!;
        
        // Extract private key from system actor
        if (systemActor.ExtensionData?.TryGetValue("privateKeyPem", out var privateKeyElement) == true)
        {
            _systemPrivateKey = privateKeyElement.GetString()!;
        }
        else if (systemActor.ExtensionData?.TryGetValue("privateKey", out var altPrivateKeyElement) == true)
        {
            _systemPrivateKey = altPrivateKeyElement.GetString()!;
        }

        Assert.NotNull(_systemPrivateKey);

        // Create authenticated client
        var httpClientFactory = new Func<HttpClient>(() => _server.CreateClient());
        _authenticatedClient = TestClientFactory.CreateAuthenticatedClient(
            httpClientFactory,
            _systemActorId,
            _systemPrivateKey);
    }

    public async Task DisposeAsync()
    {
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task AdminBackChannel_CreateUser_UserCreatedSuccessfully()
    {
        // Arrange - Create a new Person actor
        var newPerson = new Person
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")),
                new ReferenceTermDefinition(new Uri("https://w3id.org/security/v1"))
            },
            PreferredUsername = "alice",
            Name = new[] { "Alice Smith" },
            Summary = new[] { "A test user created via admin operations" }
        };

        var createActivity = new Activity
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Id = $"{_systemActorId}/activities/{Guid.NewGuid()}",
            Type = new[] { "Create" },
            Actor = new IObjectOrLink[] { new Actor { Id = _systemActorId } },
            Object = new IObjectOrLink[] { newPerson },
            Published = DateTime.UtcNow
        };

        // Act - Post to system inbox
        var response = await _authenticatedClient.PostAsync(
            new Uri($"{_server.BaseUrl}/users/sys/inbox"),
            createActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Failed with status: {response.StatusCode}");

        // Verify the user was created
        using var scope = _server.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        var createdActor = await actorRepo.GetActorByUsernameAsync("alice");

        Assert.NotNull(createdActor);
        Assert.Equal("alice", createdActor.PreferredUsername);
        Assert.Equal("Alice Smith", createdActor.Name?.FirstOrDefault());
        Assert.NotNull(createdActor.Inbox);
        Assert.NotNull(createdActor.Outbox);
        Assert.NotNull(createdActor.ExtensionData);
        
        // Verify keys were generated
        Assert.True(createdActor.ExtensionData.ContainsKey("publicKey"));
        Assert.True(createdActor.ExtensionData.ContainsKey("privateKeyPem"));
    }

    [Fact]
    public async Task AdminBackChannel_CreateDuplicateUser_FailsGracefully()
    {
        // Arrange - First create a user normally
        using (var scope = _server.Services.CreateScope())
        {
            var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "bob", _server.BaseUrl);
        }

        // Now try to create the same user via admin operations
        var newPerson = new Person
        {
            PreferredUsername = "bob",
            Name = new[] { "Bob Jones" }
        };

        var createActivity = new Activity
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Id = $"{_systemActorId}/activities/{Guid.NewGuid()}",
            Type = new[] { "Create" },
            Actor = new IObjectOrLink[] { new Actor { Id = _systemActorId } },
            Object = new IObjectOrLink[] { newPerson }
        };

        // Act
        var response = await _authenticatedClient.PostAsync(
            new Uri($"{_server.BaseUrl}/users/sys/inbox"),
            createActivity);

        // Assert - Request is accepted but operation fails internally (logged)
        // The HTTP response still shows success since it was delivered to inbox
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task AdminBackChannel_UpdateUser_UserUpdatedSuccessfully()
    {
        // Arrange - Create a user first
        using (var scope = _server.Services.CreateScope())
        {
            var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "charlie", _server.BaseUrl);
        }

        // Create an Update activity with modified user info
        // Must include all required ActivityPub fields, not just the fields being updated
        var actorId = $"{_server.BaseUrl}/users/charlie";
        var updatedPerson = new Person
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")),
                new ReferenceTermDefinition(new Uri("https://w3id.org/security/v1"))
            },
            Id = actorId,
            Type = new[] { "Person" },
            PreferredUsername = "charlie",
            Name = new[] { "Charlie Brown - Updated" },
            Summary = new[] { "Updated summary for Charlie" },
            Inbox = new Link { Href = new Uri($"{actorId}/inbox") },
            Outbox = new Link { Href = new Uri($"{actorId}/outbox") },
            Followers = new Link { Href = new Uri($"{actorId}/followers") },
            Following = new Link { Href = new Uri($"{actorId}/following") }
        };

        var updateActivity = new Activity
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Id = $"{_systemActorId}/activities/{Guid.NewGuid()}",
            Type = new[] { "Update" },
            Actor = new IObjectOrLink[] { new Actor { Id = _systemActorId } },
            Object = new IObjectOrLink[] { updatedPerson }
        };

        // Act
        var response = await _authenticatedClient.PostAsync(
            new Uri($"{_server.BaseUrl}/users/sys/inbox"),
            updateActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Verify the user was updated
        using var verifyScope = _server.Services.CreateScope();
        var verifyActorRepo = verifyScope.ServiceProvider.GetRequiredService<IActorRepository>();
        var updatedActor = await verifyActorRepo.GetActorByUsernameAsync("charlie");

        Assert.NotNull(updatedActor);
        Assert.Equal("Charlie Brown - Updated", updatedActor.Name?.FirstOrDefault());
        Assert.Equal("Updated summary for Charlie", updatedActor.Summary?.FirstOrDefault());
    }

    [Fact]
    public async Task AdminBackChannel_UpdateNonexistentUser_FailsGracefully()
    {
        // Arrange - Try to update a user that doesn't exist
        var actorId = $"{_server.BaseUrl}/users/nonexistent";
        var updatedPerson = new Person
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")),
                new ReferenceTermDefinition(new Uri("https://w3id.org/security/v1"))
            },
            Id = actorId,
            Type = new[] { "Person" },
            PreferredUsername = "nonexistent",
            Name = new[] { "Nonexistent User" },
            Inbox = new Link { Href = new Uri($"{actorId}/inbox") },
            Outbox = new Link { Href = new Uri($"{actorId}/outbox") },
            Followers = new Link { Href = new Uri($"{actorId}/followers") },
            Following = new Link { Href = new Uri($"{actorId}/following") }
        };

        var updateActivity = new Activity
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Id = $"{_systemActorId}/activities/{Guid.NewGuid()}",
            Type = new[] { "Update" },
            Actor = new IObjectOrLink[] { new Actor { Id = _systemActorId } },
            Object = new IObjectOrLink[] { updatedPerson }
        };

        // Act
        var response = await _authenticatedClient.PostAsync(
            new Uri($"{_server.BaseUrl}/users/sys/inbox"),
            updateActivity);

        // Assert - Request accepted but operation fails internally
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task AdminBackChannel_DeleteUser_UserDeletedSuccessfully()
    {
        // Arrange - Create a user first
        using (var scope = _server.Services.CreateScope())
        {
            var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "dave", _server.BaseUrl);
        }

        // Create a Delete activity
        var deleteActivity = new Activity
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Id = $"{_systemActorId}/activities/{Guid.NewGuid()}",
            Type = new[] { "Delete" },
            Actor = new IObjectOrLink[] { new Actor { Id = _systemActorId } },
            Object = new IObjectOrLink[]
            {
                new Person
                {
                    PreferredUsername = "dave"
                }
            }
        };

        // Act
        var response = await _authenticatedClient.PostAsync(
            new Uri($"{_server.BaseUrl}/users/sys/inbox"),
            deleteActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Verify the user was deleted
        using var verifyScope2 = _server.Services.CreateScope();
        var verifyActorRepo2 = verifyScope2.ServiceProvider.GetRequiredService<IActorRepository>();
        var deletedActor = await verifyActorRepo2.GetActorByUsernameAsync("dave");

        Assert.Null(deletedActor);
    }

    [Fact]
    public async Task AdminBackChannel_DeleteUserByActorId_UserDeletedSuccessfully()
    {
        // Arrange - Create a user first
        using (var scope = _server.Services.CreateScope())
        {
            var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "eve", _server.BaseUrl);
        }

        var actorId = $"{_server.BaseUrl}/users/eve";

        // Create a Delete activity using ActivityBuilder
        var builder = _activityBuilderFactory.CreateForSystemActor();
        var deleteActivity = builder.Delete(actorId);

        // Act
        var response = await _authenticatedClient.PostAsync(
            new Uri($"{_server.BaseUrl}/users/sys/inbox"),
            deleteActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Verify the user was deleted
        using var verifyScope3 = _server.Services.CreateScope();
        var verifyActorRepo3 = verifyScope3.ServiceProvider.GetRequiredService<IActorRepository>();
        var deletedActor = await verifyActorRepo3.GetActorByUsernameAsync("eve");

        Assert.Null(deletedActor);
    }

    [Fact]
    public async Task AdminBackChannel_CannotDeleteSystemActor_FailsGracefully()
    {
        // Arrange - Try to delete the system actor
        var deleteActivity = new Activity
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Id = $"{_systemActorId}/activities/{Guid.NewGuid()}",
            Type = new[] { "Delete" },
            Actor = new IObjectOrLink[] { new Actor { Id = _systemActorId } },
            Object = new IObjectOrLink[]
            {
                new Person
                {
                    PreferredUsername = "sys"
                }
            }
        };

        // Act
        var response = await _authenticatedClient.PostAsync(
            new Uri($"{_server.BaseUrl}/users/sys/inbox"),
            deleteActivity);

        // Assert - Request accepted but deletion fails internally
        Assert.True(response.IsSuccessStatusCode);

        // Verify system actor still exists
        using var verifyScope4 = _server.Services.CreateScope();
        var verifyActorRepo4 = verifyScope4.ServiceProvider.GetRequiredService<IActorRepository>();
        var systemActor = await verifyActorRepo4.GetActorByUsernameAsync("sys");

        Assert.NotNull(systemActor);
    }

    [Fact]
    public async Task AdminBackChannel_UnauthorizedActor_OperationIgnored()
    {
        // Arrange - Create an unauthorized actor
        string unauthorizedPrivateKey;
        using (var scope = _server.Services.CreateScope())
        {
            var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
            var (actor, privateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, "hacker", _server.BaseUrl);
            unauthorizedPrivateKey = privateKey;
        }

        var hackerActorId = $"{_server.BaseUrl}/users/hacker";

        // Create an unauthorized client
        var unauthorizedClient = TestClientFactory.CreateAuthenticatedClient(
            () => _server.CreateClient(),
            hackerActorId,
            unauthorizedPrivateKey);

        // Try to create a user
        var newPerson = new Person
        {
            PreferredUsername = "victim",
            Name = new[] { "Victim User" }
        };

        var createActivity = new Activity
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Id = $"{hackerActorId}/activities/{Guid.NewGuid()}",
            Type = new[] { "Create" },
            Actor = new IObjectOrLink[] { new Actor { Id = hackerActorId } },
            Object = new IObjectOrLink[] { newPerson }
        };

        // Act
        var response = await unauthorizedClient.PostAsync(
            new Uri($"{_server.BaseUrl}/users/sys/inbox"),
            createActivity);

        // Assert - Request is accepted (delivered to inbox) but operation is rejected
        Assert.True(response.IsSuccessStatusCode);

        // Verify the user was NOT created
        using var verifyScope = _server.Services.CreateScope();
        var verifyActorRepo = verifyScope.ServiceProvider.GetRequiredService<IActorRepository>();
        var victimActor = await verifyActorRepo.GetActorByUsernameAsync("victim");

        Assert.Null(victimActor);
    }
}
