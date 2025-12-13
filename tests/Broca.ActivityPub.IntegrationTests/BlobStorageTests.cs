using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.IntegrationTests.Infrastructure;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Broca.ActivityPub.IntegrationTests;

/// <summary>
/// Integration tests for blob storage functionality
/// Tests validate end-to-end HTTP interactions:
/// - Media endpoint retrieves blobs correctly
/// - Create activities with attachments work via HTTP
/// - Cross-server delivery with attachments
/// - HTTP status codes for blob operations
/// 
/// Unit tests for IBlobStorageService are in Broca.ActivityPub.UnitTests
/// </summary>
public class BlobStorageTests : TwoServerFixture
{
    [Fact]
    public async Task BlobStorage_RetrieveThroughMediaEndpoint_ReturnsCorrectContentType()
    {
        // Arrange - Seed Alice and store a blob
        using var scope = ServerA.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();

        var (alice, _) = await TestDataSeeder.SeedActorAsync(
            actorRepo,
            "alice",
            ServerA.BaseUrl);

        var blobId = $"avatar-{Guid.NewGuid()}.jpg";
        var testImageData = CreateTestImageData();
        
        using var blobStream = new MemoryStream(testImageData);
        var blobUrl = await blobStorage.StoreBlobAsync(
            "alice",
            blobId,
            blobStream,
            "image/jpeg");

        // Act - Retrieve through HTTP endpoint
        var client = ServerA.CreateClient();
        var response = await client.GetAsync($"/users/alice/media/{blobId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        
        var content = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(testImageData.Length, content.Length);
        Assert.Equal(testImageData, content);
    }

    [Fact]
    public async Task BlobStorage_CreateActivityWithAttachment_BlobUrlIsCorrect()
    {
        // Arrange - Seed Alice on Server A
        using var scope = ServerA.Services.CreateScope();
        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();

        var (alice, alicePrivateKey) = await TestDataSeeder.SeedActorAsync(
            actorRepo,
            "alice",
            ServerA.BaseUrl);

        // Store a blob first
        var blobId = $"photo-{Guid.NewGuid()}.png";
        var testImageData = CreateTestImageData();
        
        using var blobStream = new MemoryStream(testImageData);
        var blobUrl = await blobStorage.StoreBlobAsync(
            "alice",
            blobId,
            blobStream,
            "image/png");

        // Create authenticated client for Alice
        var aliceClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            alice.Id!,
            alicePrivateKey);

        var c2sHelper = new ClientToServerHelper(aliceClient, alice.Id!, ClientA);

        // Act - Create a Note with an image attachment
        var createActivity = TestDataSeeder.CreateCreateActivityWithAttachment(
            alice.Id!,
            "Check out this photo!",
            blobUrl,
            "image/png");

        var response = await c2sHelper.PostToOutboxAsync(createActivity);

        // Assert - Verify the activity was posted
        Assert.True(response.IsSuccessStatusCode);

        // Retrieve from outbox and verify attachment URL
        var s2sHelper = new ServerToServerHelper(ServerA);
        var outboxActivities = await s2sHelper.GetOutboxActivitiesAsync("alice");
        
        var postedActivity = outboxActivities
            .OfType<Create>()
            .FirstOrDefault();

        Assert.NotNull(postedActivity);
        Assert.NotNull(postedActivity.Object);
        
        // Get the Note object - it might be in a list
        var objectList = postedActivity.Object as IEnumerable<IObjectOrLink>;
        var note = objectList?.FirstOrDefault() as Note;
        
        Assert.NotNull(note);
        Assert.NotNull(note.Attachment);
        
        // Verify attachment has correct URL
        var attachment = note.Attachment.FirstOrDefault() as Document;
        Assert.NotNull(attachment);
        Assert.NotNull(attachment.Url);
        Assert.Contains(blobUrl, attachment.Url?.First()?.Href?.ToString() ?? "");
    }

    [Fact]
    public async Task BlobStorage_DeliveredActivityWithAttachment_UriIsAccessible()
    {
        // Arrange - Seed users on different servers
        using var scopeA = ServerA.Services.CreateScope();
        using var scopeB = ServerB.Services.CreateScope();
        
        var actorRepoA = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
        var actorRepoB = scopeB.ServiceProvider.GetRequiredService<IActorRepository>();
        var blobStorageA = scopeA.ServiceProvider.GetRequiredService<IBlobStorageService>();

        var (alice, alicePrivateKey) = await TestDataSeeder.SeedActorAsync(
            actorRepoA,
            "alice",
            ServerA.BaseUrl);

        var (bob, bobPrivateKey) = await TestDataSeeder.SeedActorAsync(
            actorRepoB,
            "bob",
            ServerB.BaseUrl);

        // Alice stores an image
        var blobId = $"shared-image-{Guid.NewGuid()}.png";
        var testImageData = CreateTestImageData();
        
        using var blobStream = new MemoryStream(testImageData);
        var blobUrl = await blobStorageA.StoreBlobAsync(
            "alice",
            blobId,
            blobStream,
            "image/png");

        // Alice creates a Create activity with the image
        var aliceClient = TestClientFactory.CreateAuthenticatedClient(
            () => ServerA.CreateClient(),
            alice.Id!,
            alicePrivateKey);

        var c2sHelper = new ClientToServerHelper(aliceClient, alice.Id!, ClientA);

        // Create activity addressed to Bob
        var createActivity = TestDataSeeder.CreateCreateActivityWithAttachmentToRecipient(
            alice.Id!,
            bob.Id!,
            "Sharing this with you!",
            blobUrl,
            "image/png");

        await c2sHelper.PostToOutboxAsync(createActivity);

        // Act - Wait for delivery to Bob's inbox on Server B
        var s2sHelper = new ServerToServerHelper(ServerB, sendingServer: ServerA);
        var deliveredActivity = await s2sHelper.WaitForInboxActivityByTypeAsync(
            "bob",
            "Create",
            TimeSpan.FromSeconds(15));

        // Assert - Verify the delivered activity has the attachment
        Assert.NotNull(deliveredActivity);
        var create = deliveredActivity as Create;
        Assert.NotNull(create);
        Assert.NotNull(create.Object);
        
        // Get the Note object - it might be in a list
        var objectList = create.Object as IEnumerable<IObjectOrLink>;
        var note = objectList?.FirstOrDefault() as Note;
        
        Assert.NotNull(note);
        Assert.NotNull(note.Attachment);
        
        var attachment = note.Attachment.FirstOrDefault() as Document;
        Assert.NotNull(attachment);
        Assert.NotNull(attachment.Url);
        
        var attachmentUrl = attachment.Url.First()?.Href?.ToString();
        Assert.NotNull(attachmentUrl);
        Assert.Contains(ServerA.BaseUrl, attachmentUrl);
        Assert.Contains("media", attachmentUrl);

        // Verify the blob can be retrieved from Alice's server
        // In a real federated scenario, remote servers would fetch directly from the origin
        var retrieveResponse = await ClientA.GetAsync(attachmentUrl);
        
        Assert.Equal(HttpStatusCode.OK, retrieveResponse.StatusCode);
        Assert.Equal("image/png", retrieveResponse.Content.Headers.ContentType?.MediaType);
        
        var retrievedData = await retrieveResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(testImageData.Length, retrievedData.Length);
    }

    [Fact]
    public async Task BlobStorage_NonExistentBlob_Returns404()
    {
        // Arrange
        var client = ServerA.CreateClient();

        // Act - Try to retrieve a blob that doesn't exist
        var response = await client.GetAsync($"/users/alice/media/nonexistent-{Guid.NewGuid()}.png");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Creates test image data - a simple PNG-like byte array
    /// </summary>
    private static byte[] CreateTestImageData(int seed = 1)
    {
        // Create a simple test data pattern
        var data = new byte[1024];
        var random = new Random(seed);
        random.NextBytes(data);
        
        // Add PNG header signature to make it look more realistic
        data[0] = 0x89;
        data[1] = 0x50;
        data[2] = 0x4E;
        data[3] = 0x47;
        data[4] = 0x0D;
        data[5] = 0x0A;
        data[6] = 0x1A;
        data[7] = 0x0A;
        
        return data;
    }
}
