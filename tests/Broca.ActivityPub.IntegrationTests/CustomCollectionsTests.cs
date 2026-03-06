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
        var collectionUri = new Uri($"{_server.BaseUrl}/users/{username}/collections/featured");
        var collection = await _authenticatedClient.GetAsync<OrderedCollection>(collectionUri);

        // Assert
        Assert.NotNull(collection);
        Assert.Equal(0u, collection.TotalItems ?? 0u);
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
        
        // Should contain broca:collections catalog pointer
        Assert.Contains("\"broca:collections\"", content);
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
        var collectionNotes = await GetCollectionItemsAsync<Note>($"/users/{username}/collections/featured");

        Assert.Contains(collectionNotes, n => n.Content?.Contains("This is a featured post!") == true);
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
        var featuredNotes = await GetCollectionItemsAsync<Note>($"/users/{username}/collections/featured");
        var noteId = featuredNotes.FirstOrDefault()?.Id;

        Assert.NotNull(noteId);

        // Use ActivityBuilder to create an Add activity with just a link reference
        var favoritesCollectionUrl = $"{_server.BaseUrl}/users/{username}/collections/favorites";
        var addActivity = TestDataSeeder.CreateAdd(userActorId, noteId, favoritesCollectionUrl);

        // Act - Post Add activity to outbox
        var response = await PostToOutboxAsync(username, addActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Verify collection contains the object with full content
        var favNotes = await GetCollectionItemsAsync<Note>($"/users/{username}/collections/favorites");

        Assert.Contains(favNotes, n => n.Content?.Contains("Original post content") == true);
        Assert.Contains(favNotes, n => n.Id == noteId);
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

        // Get the note ID from the collection
        var featuredNotes = await GetCollectionItemsAsync<Note>($"/users/{username}/collections/featured");
        var noteId = featuredNotes.FirstOrDefault()?.Id;

        Assert.NotNull(noteId);

        // Use ActivityBuilder to create Remove activity
        var removeActivity = TestDataSeeder.CreateRemove(userActorId, noteId, featuredCollectionUrl);

        // Act
        var response = await PostToOutboxAsync(username, removeActivity);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Verify collection no longer contains the object
        var remainingNotes = await GetCollectionItemsAsync<Note>($"/users/{username}/collections/featured");

        Assert.DoesNotContain(remainingNotes, n => n.Content?.Contains("Post to remove") == true);
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

        // Act - Get collection items via the ActivityPub client (handles paging automatically)
        var notes = await GetCollectionItemsAsync<Note>($"/users/{username}/collections/featured");

        // Assert
        Assert.Equal(3, notes.Count);
        Assert.Contains(notes, n => n.Content?.Contains("Featured post number 1") == true);
        Assert.Contains(notes, n => n.Content?.Contains("Featured post number 2") == true);
        Assert.Contains(notes, n => n.Content?.Contains("Featured post number 3") == true);
    }

    [Fact]
    public async Task ActorDocument_WithFeaturedCollection_ExposesFeaturedProperty()
    {
        // Arrange - Create a user with a featured collection
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);
        await CreateTestCollectionAsync(username, "featured", "Featured Posts");

        // Act - Get the actor document
        var response = await _client.GetAsync($"/users/{username}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var actorDoc = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);
        
        // Verify "featured" property exists at root level (de facto fediverse standard)
        Assert.True(actorDoc.TryGetProperty("featured", out var featuredProp), 
            "Actor document should have 'featured' property for Mastodon compatibility");
        var featuredUrl = featuredProp.GetString();
        Assert.NotNull(featuredUrl);
        Assert.Contains($"/users/{username}/collections/featured", featuredUrl);
        
        // Verify "broca:featured" also exists (our namespaced version for consistency)
        Assert.True(actorDoc.TryGetProperty("broca:featured", out var brocaFeaturedProp),
            "Actor document should have 'broca:featured' property for consistency");
        var brocaFeaturedUrl = brocaFeaturedProp.GetString();
        Assert.NotNull(brocaFeaturedUrl);
        Assert.Contains($"/users/{username}/collections/featured", brocaFeaturedUrl);
    }

    [Fact]
    public async Task ActorDocument_WithoutFeaturedCollection_NoFeaturedProperty()
    {
        // Arrange - Create a user without a featured collection
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);

        // Act - Get the actor document
        var response = await _client.GetAsync($"/users/{username}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        
        // Verify "featured" property does NOT exist
        Assert.DoesNotContain("\"featured\":", content);
    }

    [Fact]
    public async Task ActorDocument_WithOtherPublicCollections_OnlyFeaturedAtRoot()
    {
        // Arrange - Create a user with multiple public collections
        var username = $"testuser_{Guid.NewGuid():N}";
        await CreateTestUserAsync(username);
        await CreateTestCollectionAsync(username, "featured", "Featured Posts");
        await CreateTestCollectionAsync(username, "photography", "Photography");
        await CreateTestCollectionAsync(username, "bookmarks", "Bookmarks");

        // Act - Get the actor document
        var response = await _client.GetAsync($"/users/{username}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        
        // Only "featured" should be at root level (not photography or bookmarks)
        Assert.Contains("\"featured\":", content);
        Assert.DoesNotContain("\"photography\":", content);
        Assert.DoesNotContain("\"bookmarks\":", content);
        
        // All should have broca: prefixed versions
        Assert.Contains("\"broca:featured\":", content);
        Assert.Contains("\"broca:photography\":", content);
        Assert.Contains("\"broca:bookmarks\":", content);
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
        using var scope = _server.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        var actor = await actorRepo.GetActorByUsernameAsync(username);
        Assert.NotNull(actor);

        var privateKey = actor.ExtensionData!["privateKeyPem"].GetString()!;
        var client = TestClientFactory.CreateAuthenticatedClient(
            () => _server.CreateClient(),
            actor.Id!,
            privateKey);

        return await client.PostAsync<Activity>(
            new Uri($"{_server.BaseUrl}/users/{username}/outbox"),
            activity);
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

    private async Task<List<T>> GetCollectionItemsAsync<T>(string collectionPath)
    {
        var client = TestClientFactory.CreateClient(() => _server.CreateClient());
        var uri = new Uri($"{_server.BaseUrl}{collectionPath}");
        var items = new List<T>();
        await foreach (var item in client.GetCollectionAsync<T>(uri))
            items.Add(item);
        return items;
    }
}
