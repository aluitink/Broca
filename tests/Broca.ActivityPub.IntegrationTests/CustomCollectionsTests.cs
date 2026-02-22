using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.IntegrationTests.Infrastructure;
using Broca.ActivityPub.Persistence.InMemory;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Broca.ActivityPub.IntegrationTests;

public class CustomCollectionsTests : IAsyncLifetime
{
    private BrocaTestServer _server = null!;
    private string _systemActorId = null!;
    private string _systemPrivateKey = null!;
    private IActivityPubClient _authenticatedClient = null!;
    private HttpClient _client = null!;
    private readonly JsonSerializerOptions _jsonOptions;

    public CustomCollectionsTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task InitializeAsync()
    {
        _server = new BrocaTestServer(
            "https://test-server.local",
            "test-server.local",
            "TestServer");

        await _server.InitializeAsync();
        _client = _server.CreateClient();

        // Get system actor credentials
        using var scope = _server.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
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
    public async Task CreateCollection_ViaActivityPub_CreatesManualCollection()
    {
        // Arrange - Create a test user first
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);

        // Create a Collection object with Broca extension
        var collectionDefinition = new CustomCollectionDefinition
        {
            Id = "featured",
            Name = "Featured Posts",
            Description = "My hand-picked featured posts",
            Type = CollectionType.Manual,
            Visibility = CollectionVisibility.Public
        };

        var collection = new Collection
        {
            Type = new List<string> { "Collection" },
            Name = new List<string> { "Featured Posts" },
            AttributedTo = new List<IObjectOrLink> { new Link { Href = new Uri($"{_server.BaseUrl}/users/{username}") } },
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["collectionDefinition"] = JsonSerializer.SerializeToElement(collectionDefinition, _jsonOptions)
            }
        };

        var createActivity = TestDataSeeder.CreateCreate(_systemActorId, collection);

        // Act - Post to system inbox
        var response = await PostToSystemInboxAsync(createActivity);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        // Verify collection was created
        var getResponse = await _client.GetAsync($"/users/{username}/collections/featured");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task CreateCollection_ViaActivityPub_CreatesQueryCollection()
    {
        // Arrange - Create a test user first
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);

        // Create a query collection for posts with attachments
        var collectionDefinition = new CustomCollectionDefinition
        {
            Id = "media",
            Name = "Media Posts",
            Description = "Posts with media attachments",
            Type = CollectionType.Query,
            Visibility = CollectionVisibility.Public,
            QueryFilter = new CollectionQueryFilter
            {
                HasAttachment = true
            }
        };

        var collection = new Collection
        {
            Type = new List<string> { "Collection" },
            Name = new List<string> { "Media Posts" },
            AttributedTo = new List<IObjectOrLink> { new Link { Href = new Uri($"{_server.BaseUrl}/users/{username}") } },
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["collectionDefinition"] = JsonSerializer.SerializeToElement(collectionDefinition, _jsonOptions)
            }
        };

        var createActivity = TestDataSeeder.CreateCreate(_systemActorId, collection);

        // Act
        var response = await PostToSystemInboxAsync(createActivity);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        // Verify collection was created
        var getResponse = await _client.GetAsync($"/users/{username}/collections/media");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetCollections_ReturnsCollectionsCatalog()
    {
        // Arrange - Create a test user with collections
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);
        await CreateTestCollectionAsync(username, "featured", "Featured Posts");
        await CreateTestCollectionAsync(username, "pinned", "Pinned Posts");

        // Act
        var response = await _client.GetAsync($"/users/{username}/collections");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Featured Posts", content);
        Assert.Contains("Pinned Posts", content);
    }

    [Fact]
    public async Task GetCollection_ReturnsCollectionItems()
    {
        // Arrange - Create a test user with a collection
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);
        await CreateTestCollectionAsync(username, "featured", "Featured Posts");

        // Act
        var response = await _client.GetAsync($"/users/{username}/collections/featured");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var collection = JsonSerializer.Deserialize<OrderedCollectionPage>(content, _jsonOptions);
        
        Assert.NotNull(collection);
        Assert.Equal("OrderedCollectionPage", collection.Type?.FirstOrDefault());
    }

    [Fact]
    public async Task GetCollectionDefinition_ReturnsDefinition()
    {
        // Arrange - Create a test user with a collection
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);
        await CreateTestCollectionAsync(username, "featured", "Featured Posts");

        // Act
        var response = await _client.GetAsync($"/users/{username}/collections/featured/definition");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var definition = await response.Content.ReadFromJsonAsync<CustomCollectionDefinition>(_jsonOptions);
        
        Assert.NotNull(definition);
        Assert.Equal("featured", definition.Id);
        Assert.Equal("Featured Posts", definition.Name);
        Assert.Equal(CollectionType.Manual, definition.Type);
    }

    [Fact]
    public async Task ActorProfile_IncludesPublicCollections()
    {
        // Arrange - Create a test user with collections
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);
        await CreateTestCollectionAsync(username, "featured", "Featured Posts");

        // Act
        var response = await _client.GetAsync($"/users/{username}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        
        // Should contain collections catalog endpoint (like inbox, outbox)
        Assert.Contains("\"collections\"", content);
        Assert.Contains("/collections\"", content);
        
        // Should contain individual collection with broca: prefix
        Assert.Contains("\"broca:featured\"", content);
        Assert.Contains("/collections/featured", content);
    }

    [Fact]
    public async Task PrivateCollection_NotVisibleWithoutAuth()
    {
        // Arrange - Create a test user with a private collection
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);
        
        var privateCollection = new CustomCollectionDefinition
        {
            Id = "private",
            Name = "Private Posts",
            Type = CollectionType.Manual,
            Visibility = CollectionVisibility.Private
        };
        
        await CreateTestCollectionAsync(username, privateCollection);

        // Act - Try to access without authentication
        var response = await _client.GetAsync($"/users/{username}/collections/private");

        // Assert - Should be forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddActivity_WithFullObject_AddsToCollectionAndStoresObject()
    {
        // Arrange - Create a test user with a manual collection
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);
        await CreateTestCollectionAsync(username, "featured", "Featured Posts");

        // Create a Note object to add (no ID - server will generate it)
        var note = new Note
        {
            Type = new List<string> { "Note" },
            Content = new List<string> { "This is a featured post!" },
            AttributedTo = new List<IObjectOrLink> { new Link { Href = new Uri($"{_server.BaseUrl}/users/{username}") } },
            Published = DateTime.UtcNow
        };

        // Use ActivityBuilder to create Add activity
        var userActorId = $"{_server.BaseUrl}/users/{username}";
        var targetCollectionUrl = $"{_server.BaseUrl}/users/{username}/collections/featured";
        var addActivity = TestDataSeeder.CreateAdd(userActorId, note, targetCollectionUrl);

        // Act - User posts Add activity to their outbox
        var response = await PostToOutboxAsync(username, addActivity);

        // Assert - Activity was accepted
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");

        // Verify collection contains the object
        var collectionResponse = await _client.GetAsync($"/users/{username}/collections/featured");
        Assert.Equal(HttpStatusCode.OK, collectionResponse.StatusCode);
        var collectionContent = await collectionResponse.Content.ReadAsStringAsync();
        
        // Collection should return the full object, not just the ID
        Assert.Contains("This is a featured post!", collectionContent);
    }

    [Fact]
    public async Task AddActivity_WithLinkReference_FetchesAndStoresObject()
    {
        // Arrange - Create a test user with two manual collections
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);
        await CreateTestCollectionAsync(username, "featured", "Featured Posts");
        await CreateTestCollectionAsync(username, "favorites", "Favorite Posts");

        // First, create a post by adding it to the featured collection
        // This will store the object and give it an ID
        var note = new Note
        {
            Type = new List<string> { "Note" },
            Content = new List<string> { "Original post content" },
            AttributedTo = new List<IObjectOrLink> { new Link { Href = new Uri($"{_server.BaseUrl}/users/{username}") } },
            Published = DateTime.UtcNow
        };

        var userActorId = $"{_server.BaseUrl}/users/{username}";
        var featuredCollectionUrl = $"{_server.BaseUrl}/users/{username}/collections/featured";
        var addToTempCollection = TestDataSeeder.CreateAdd(userActorId, note, featuredCollectionUrl);

        var addResponse = await PostToOutboxAsync(username, addToTempCollection);
        Assert.True(addResponse.IsSuccessStatusCode);

        // Get the created note's ID from the featured collection
        var featuredResponse = await _client.GetAsync($"/users/{username}/collections/featured");
        var featuredContent = await featuredResponse.Content.ReadAsStringAsync();
        
        // Parse to get the note ID
        using var doc = JsonDocument.Parse(featuredContent);
        var root = doc.RootElement;
        
        string? noteId = null;
        
        if (root.TryGetProperty("orderedItems", out var orderedItemsElement))
        {
            // Handle both single object and array
            if (orderedItemsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in orderedItemsElement.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var typeElement) && 
                        typeElement.GetString() == "Note" &&
                        item.TryGetProperty("id", out var idElement))
                    {
                        noteId = idElement.GetString();
                        break;
                    }
                }
            }
            else if (orderedItemsElement.ValueKind == JsonValueKind.Object)
            {
                if (orderedItemsElement.TryGetProperty("type", out var typeElement) && 
                    typeElement.GetString() == "Note" &&
                    orderedItemsElement.TryGetProperty("id", out var idElement))
                {
                    noteId = idElement.GetString();
                }
            }
        }
        
        Assert.NotNull(noteId);

        // Use ActivityBuilder to create an Add activity with just a link reference
        var favoritesCollectionUrl = $"{_server.BaseUrl}/users/{username}/collections/favorites";
        var addActivity = TestDataSeeder.CreateAdd(userActorId, noteId, favoritesCollectionUrl);

        // Act - Post Add activity to outbox
        var response = await PostToOutboxAsync(username, addActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Verify collection contains the object with full content
        var collectionResponse = await _client.GetAsync($"/users/{username}/collections/favorites");
        Assert.Equal(HttpStatusCode.OK, collectionResponse.StatusCode);
        var collectionContent = await collectionResponse.Content.ReadAsStringAsync();
        
        // Should return the full object
        Assert.Contains("Original post content", collectionContent);
        Assert.Contains(noteId, collectionContent);
    }

    [Fact]
    public async Task AddActivity_ToQueryCollection_ReturnsError()
    {
        // Arrange - Create a test user with a query collection
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);
        
        var queryCollection = new CustomCollectionDefinition
        {
            Id = "media",
            Name = "Media Posts",
            Type = CollectionType.Query,
            Visibility = CollectionVisibility.Public,
            QueryFilter = new CollectionQueryFilter { HasAttachment = true }
        };
        
        await CreateTestCollectionAsync(username, queryCollection);

        // Create an Add activity
        var note = new Note
        {
            Type = new List<string> { "Note" },
            Content = new List<string> { "Test post" }
        };

        var userActorId = $"{_server.BaseUrl}/users/{username}";
        var mediaCollectionUrl = $"{_server.BaseUrl}/users/{username}/collections/media";
        var addActivity = TestDataSeeder.CreateAdd(userActorId, note, mediaCollectionUrl);

        // Act - Post to outbox
        var response = await PostToOutboxAsync(username, addActivity);

        // Assert - Should return InternalServerError since query collections are read-only
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task RemoveActivity_RemovesFromCollection()
    {
        // Arrange - Create a test user with a manual collection containing an item
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);
        await CreateTestCollectionAsync(username, "featured", "Featured Posts");

        // Add an item first
        var note = new Note
        {
            Type = new List<string> { "Note" },
            Content = new List<string> { "Post to remove" }
        };

        var userActorId = $"{_server.BaseUrl}/users/{username}";
        var featuredCollectionUrl = $"{_server.BaseUrl}/users/{username}/collections/featured";
        var addActivity = TestDataSeeder.CreateAdd(userActorId, note, featuredCollectionUrl);

        await PostToOutboxAsync(username, addActivity);

        // Get the note ID from the collection - it should have been stored there
        var collectionResponse = await _client.GetAsync($"/users/{username}/collections/featured");
        var collectionContent = await collectionResponse.Content.ReadAsStringAsync();
        
        // Parse to get the note ID
        using var doc = JsonDocument.Parse(collectionContent);
        var root = doc.RootElement;
        
        string? noteId = null;
        
        if (root.TryGetProperty("orderedItems", out var orderedItemsElement))
        {
            // Handle both single object and array
            if (orderedItemsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in orderedItemsElement.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var typeElement) && 
                        typeElement.GetString() == "Note" &&
                        item.TryGetProperty("id", out var idElement))
                    {
                        noteId = idElement.GetString();
                        break;
                    }
                }
            }
            else if (orderedItemsElement.ValueKind == JsonValueKind.Object)
            {
                if (orderedItemsElement.TryGetProperty("type", out var typeElement) && 
                    typeElement.GetString() == "Note" &&
                    orderedItemsElement.TryGetProperty("id", out var idElement))
                {
                    noteId = idElement.GetString();
                }
            }
        }
        
        Assert.NotNull(noteId);

        // Use ActivityBuilder to create Remove activity
        var removeActivity = TestDataSeeder.CreateRemove(userActorId, noteId, featuredCollectionUrl);

        // Act
        var response = await PostToOutboxAsync(username, removeActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Verify collection no longer contains the object
        var collectionResponseAfter = await _client.GetAsync($"/users/{username}/collections/featured");
        var collectionContentAfter = await collectionResponseAfter.Content.ReadAsStringAsync();
        
        // The removed item should not be in the collection
        Assert.DoesNotContain("Post to remove", collectionContentAfter);
    }

    [Fact]
    public async Task GetCollectionItems_ReturnsFullObjects_NotJustLinks()
    {
        // Arrange - Create a user with a collection
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);
        await CreateTestCollectionAsync(username, "featured", "Featured Posts");

        // Add multiple items
        var userActorId = $"{_server.BaseUrl}/users/{username}";
        var featuredCollectionUrl = $"{_server.BaseUrl}/users/{username}/collections/featured";
        
        for (int i = 0; i < 3; i++)
        {
            var note = new Note
            {
                Type = new List<string> { "Note" },
                Content = new List<string> { $"Featured post number {i + 1}" },
                AttributedTo = new List<IObjectOrLink> { new Link { Href = new Uri($"{_server.BaseUrl}/users/{username}") } },
                Published = DateTime.UtcNow.AddMinutes(-i)
            };

            var addActivity = TestDataSeeder.CreateAdd(userActorId, note, featuredCollectionUrl);

            await PostToOutboxAsync(username, addActivity);
        }

        // Act - Get collection items
        var response = await _client.GetAsync($"/users/{username}/collections/featured");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        
        // Verify all objects are returned with full content
        Assert.Contains("Featured post number 1", content);
        Assert.Contains("Featured post number 2", content);
        Assert.Contains("Featured post number 3", content);
        
        // Verify it's an OrderedCollectionPage
        var collection = JsonSerializer.Deserialize<OrderedCollectionPage>(content, _jsonOptions);
        Assert.NotNull(collection);
        Assert.NotNull(collection.OrderedItems);
        Assert.Equal(3, collection.OrderedItems.Count());
    }

    // Helper methods

    private async Task CreateTestUserAsync(string username)
    {
        var actor = new Person
        {
            Type = new List<string> { "Person" },
            PreferredUsername = username,
            Name = new List<string> { $"Test User {username}" }
        };

        var createActivity = TestDataSeeder.CreateCreate(_systemActorId, actor);

        await PostToSystemInboxAsync(createActivity);
    }

    private async Task CreateTestCollectionAsync(string username, string collectionId, string collectionName)
    {
        var definition = new CustomCollectionDefinition
        {
            Id = collectionId,
            Name = collectionName,
            Type = CollectionType.Manual,
            Visibility = CollectionVisibility.Public
        };

        await CreateTestCollectionAsync(username, definition);
    }

    private async Task CreateTestCollectionAsync(string username, CustomCollectionDefinition definition)
    {
        var collection = new Collection
        {
            Type = new List<string> { "Collection" },
            Name = new List<string> { definition.Name },
            AttributedTo = new List<IObjectOrLink> { new Link { Href = new Uri($"{_server.BaseUrl}/users/{username}") } },
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["collectionDefinition"] = JsonSerializer.SerializeToElement(definition, _jsonOptions)
            }
        };

        var createActivity = TestDataSeeder.CreateCreate(_systemActorId, collection);

        await PostToSystemInboxAsync(createActivity);
    }

    private async Task<HttpResponseMessage> PostToOutboxAsync(string username, Activity activity)
    {
        var json = JsonSerializer.Serialize<IObjectOrLink>(activity, _jsonOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/activity+json");
        
        return await _client.PostAsync($"/users/{username}/outbox", content);
    }

    private async Task<HttpResponseMessage> PostToSystemInboxAsync(Activity activity)
    {
        var response = await _authenticatedClient.PostAsync(
            new Uri($"{_server.BaseUrl}/users/sys/inbox"),
            activity);
        
        return new HttpResponseMessage(response.IsSuccessStatusCode ? HttpStatusCode.Accepted : HttpStatusCode.BadRequest);
    }

    private async Task<HttpResponseMessage> PostToUserInboxAsync(string username, Activity activity)
    {
        var response = await _authenticatedClient.PostAsync(
            new Uri($"{_server.BaseUrl}/users/{username}/inbox"),
            activity);
        
        return new HttpResponseMessage(response.IsSuccessStatusCode ? HttpStatusCode.Accepted : HttpStatusCode.BadRequest);
    }
}
