using System.Net;
using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Server.Extensions;
using Broca.ActivityPub.IntegrationTests.Infrastructure;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.IntegrationTests;

public class CollectionSearchFixture : MultiServerTestFixture
{
    protected BrocaTestServer Server => GetServer("Server");
    protected HttpClient Client => GetClient("Server");

    protected override async Task SetupServersAsync()
    {
        await CreateServerAsync("Server", "https://search.test", "search.test",
            services => services.AddCollectionSearch());
    }
}

public class CollectionSearchTests : CollectionSearchFixture
{
    private async Task<(string actorId, string privateKey)> SeedActorWithNotes(string username, params string[] contents)
    {
        using var scope = Server.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

        var (actor, privateKey) = await TestDataSeeder.SeedActorAsync(actorRepo, username, Server.BaseUrl);

        for (int i = 0; i < contents.Length; i++)
        {
            var create = TestDataSeeder.CreateCreateActivity(actor.Id!, contents[i]);
            await activityRepo.SaveOutboxActivityAsync(username, $"{actor.Id}/activities/{i}", create);
        }

        return (actor.Id!, privateKey);
    }

    [Fact]
    public async Task Outbox_WithSearchParam_FiltersContent()
    {
        var (actorId, _) = await SeedActorWithNotes("alice",
            "Hello world from Alice",
            "Cats are great",
            "Another hello to the world");

        var response = await Client.GetAsync(
            "https://search.test/users/alice/outbox?page=0&limit=20&$search=hello");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var totalItems = root.GetProperty("totalItems").GetInt32();
        Assert.Equal(2, totalItems);
    }

    [Fact]
    public async Task Outbox_WithFilterParam_FiltersByType()
    {
        using var scope = Server.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

        var (actor, _) = await TestDataSeeder.SeedActorAsync(actorRepo, "bob", Server.BaseUrl);

        // Add a Create activity
        var create = TestDataSeeder.CreateCreateActivity(actor.Id!, "A note");
        await activityRepo.SaveOutboxActivityAsync("bob", $"{actor.Id}/activities/1", create);

        // Add a Like activity
        var like = TestDataSeeder.CreateLike(actor.Id!, "https://other.com/notes/1");
        await activityRepo.SaveOutboxActivityAsync("bob", $"{actor.Id}/activities/2", like);

        var response = await Client.GetAsync(
            "https://search.test/users/bob/outbox?page=0&limit=20&$filter=type eq 'Create'");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var totalItems = doc.RootElement.GetProperty("totalItems").GetInt32();
        Assert.Equal(1, totalItems);
    }

    [Fact]
    public async Task Outbox_WithContainsFilter_FindsMatchingContent()
    {
        var (actorId, _) = await SeedActorWithNotes("carol",
            "I love programming in C#",
            "Cooking recipes for dinner",
            "Advanced programming patterns");

        var response = await Client.GetAsync(
            "https://search.test/users/carol/outbox?page=0&limit=20&$filter=contains(content, 'programming')");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var totalItems = doc.RootElement.GetProperty("totalItems").GetInt32();
        Assert.Equal(2, totalItems);
    }

    [Fact]
    public async Task Outbox_WithOrderBy_SortsResults()
    {
        var (actorId, _) = await SeedActorWithNotes("dave",
            "First post",
            "Second post",
            "Third post");

        var response = await Client.GetAsync(
            "https://search.test/users/dave/outbox?page=0&limit=20&$orderby=published desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("orderedItems", out _));
    }

    [Fact]
    public async Task Outbox_WithCombinedFilterAndSearch_BothApplied()
    {
        using var scope = Server.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

        var (actor, _) = await TestDataSeeder.SeedActorAsync(actorRepo, "eve", Server.BaseUrl);

        var create1 = TestDataSeeder.CreateCreateActivity(actor.Id!, "Hello from my note");
        await activityRepo.SaveOutboxActivityAsync("eve", $"{actor.Id}/activities/1", create1);

        var create2 = TestDataSeeder.CreateCreateActivity(actor.Id!, "Goodbye note");
        await activityRepo.SaveOutboxActivityAsync("eve", $"{actor.Id}/activities/2", create2);

        var like = TestDataSeeder.CreateLike(actor.Id!, "https://other.com/notes/1");
        await activityRepo.SaveOutboxActivityAsync("eve", $"{actor.Id}/activities/3", like);

        var response = await Client.GetAsync(
            "https://search.test/users/eve/outbox?page=0&limit=20&$filter=type eq 'Create'&$search=hello");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var totalItems = doc.RootElement.GetProperty("totalItems").GetInt32();
        Assert.Equal(1, totalItems);
    }

    [Fact]
    public async Task Outbox_NoSearchParams_NormalPaginatedResponse()
    {
        var (actorId, _) = await SeedActorWithNotes("frank", "Just a normal post");

        var response = await Client.GetAsync(
            "https://search.test/users/frank/outbox?page=0&limit=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("orderedItems", out _));
    }

    [Fact]
    public async Task Outbox_NoParams_ReturnsCollectionWrapper()
    {
        var (actorId, _) = await SeedActorWithNotes("grace", "A post");

        var response = await Client.GetAsync("https://search.test/users/grace/outbox");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("first", out _));
        Assert.False(doc.RootElement.TryGetProperty("orderedItems", out _));
    }

    [Fact]
    public async Task Outbox_SearchWithPagination_ReturnsCorrectPage()
    {
        var contents = Enumerable.Range(1, 5)
            .Select(i => $"Searchable post number {i}")
            .ToArray();

        var (actorId, _) = await SeedActorWithNotes("hank", contents);

        var response = await Client.GetAsync(
            "https://search.test/users/hank/outbox?page=0&limit=2&$search=searchable");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var totalItems = doc.RootElement.GetProperty("totalItems").GetInt32();
        Assert.Equal(5, totalItems);

        if (doc.RootElement.TryGetProperty("orderedItems", out var items))
        {
            Assert.Equal(2, items.GetArrayLength());
        }
    }

    [Fact]
    public async Task Outbox_SearchOnlyParamWithoutPage_ReturnsFilteredPage()
    {
        var (actorId, _) = await SeedActorWithNotes("iris",
            "Match this content",
            "Unrelated content",
            "Match this too");

        var response = await Client.GetAsync(
            "https://search.test/users/iris/outbox?$search=match");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var totalItems = doc.RootElement.GetProperty("totalItems").GetInt32();
        Assert.Equal(2, totalItems);
    }

    [Fact]
    public async Task Outbox_InvalidFilter_Returns400()
    {
        var (actorId, _) = await SeedActorWithNotes("jake", "A post");

        var response = await Client.GetAsync(
            "https://search.test/users/jake/outbox?page=0&limit=20&$filter=invalid !!! syntax");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Inbox_WithSearch_FiltersContent()
    {
        using var scope = Server.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

        var (actor, _) = await TestDataSeeder.SeedActorAsync(actorRepo, "kate", Server.BaseUrl);

        var create1 = TestDataSeeder.CreateCreateActivity("https://other.com/users/someone", "Hello Kate!");
        await activityRepo.SaveInboxActivityAsync("kate", "https://other.com/activities/1", create1);

        var create2 = TestDataSeeder.CreateCreateActivity("https://other.com/users/someone", "Goodbye Kate!");
        await activityRepo.SaveInboxActivityAsync("kate", "https://other.com/activities/2", create2);

        var response = await Client.GetAsync(
            "https://search.test/users/kate/inbox?page=0&limit=20&$search=hello");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var totalItems = doc.RootElement.GetProperty("totalItems").GetInt32();
        Assert.Equal(1, totalItems);
    }

    [Fact]
    public async Task PaginationLinks_IncludeSearchParams()
    {
        var contents = Enumerable.Range(1, 5)
            .Select(i => $"Searchable item {i}")
            .ToArray();

        var (actorId, _) = await SeedActorWithNotes("leo", contents);

        var response = await Client.GetAsync(
            "https://search.test/users/leo/outbox?page=0&limit=2&$search=searchable");

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("next", out var next))
        {
            var nextUrl = next.GetString() ?? next.GetProperty("href").GetString();
            Assert.Contains("$search=", nextUrl);
            Assert.Contains("page=1", nextUrl);
        }
    }
}
