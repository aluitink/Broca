using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.InMemory;
using KristofferStrube.ActivityStreams;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Broca.ActivityPub.IntegrationTests;

public class CustomCollectionsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public CustomCollectionsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
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

        var createActivity = new Create
        {
            Type = new List<string> { "Create" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri($"http://localhost/users/{username}") } },
            Object = new List<IObjectOrLink>
            {
                new Collection
                {
                    Type = new List<string> { "Collection" },
                    Name = new List<string> { "Featured Posts" },
                    AttributedTo = new List<IObjectOrLink> { new Link { Href = new Uri($"http://localhost/users/{username}") } },
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["collectionDefinition"] = JsonSerializer.SerializeToElement(collectionDefinition, _jsonOptions)
                    }
                }
            }
        };

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

        var createActivity = new Create
        {
            Type = new List<string> { "Create" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri($"http://localhost/users/{username}") } },
            Object = new List<IObjectOrLink>
            {
                new Collection
                {
                    Type = new List<string> { "Collection" },
                    Name = new List<string> { "Media Posts" },
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["collectionDefinition"] = JsonSerializer.SerializeToElement(collectionDefinition, _jsonOptions)
                    }
                }
            }
        };

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
        
        // Should contain collections metadata
        Assert.Contains("broca:collections", content);
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

    // Helper methods

    private async Task CreateTestUserAsync(string username)
    {
        var actor = new Person
        {
            Type = new List<string> { "Person" },
            PreferredUsername = username,
            Name = new List<string> { $"Test User {username}" }
        };

        var createActivity = new Create
        {
            Type = new List<string> { "Create" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri("http://localhost/users/system") } },
            Object = new List<IObjectOrLink> { actor }
        };

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
        var createActivity = new Create
        {
            Type = new List<string> { "Create" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri($"http://localhost/users/{username}") } },
            Object = new List<IObjectOrLink>
            {
                new Collection
                {
                    Type = new List<string> { "Collection" },
                    Name = new List<string> { definition.Name },
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["collectionDefinition"] = JsonSerializer.SerializeToElement(definition, _jsonOptions)
                    }
                }
            }
        };

        await PostToSystemInboxAsync(createActivity);
    }

    private async Task<HttpResponseMessage> PostToSystemInboxAsync(Activity activity)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/users/system/inbox");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/activity+json"));
        request.Content = JsonContent.Create(activity, new MediaTypeHeaderValue("application/activity+json"), _jsonOptions);

        return await _client.SendAsync(request);
    }
}
