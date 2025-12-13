using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.InMemory;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.UnitTests;

/// <summary>
/// Unit tests for IBlobStorageService implementations
/// These tests focus on service-level behavior without HTTP layer
/// </summary>
public class BlobStorageServiceTests
{
    private readonly IBlobStorageService _blobStorage;
    private const string BaseUrl = "https://test.example.com";

    public BlobStorageServiceTests()
    {
        // Using InMemoryBlobStorageService for unit testing
        var options = Options.Create(new ActivityPubServerOptions { BaseUrl = BaseUrl });
        _blobStorage = new InMemoryBlobStorageService(options);
    }

    [Fact]
    public async Task StoreBlobAsync_WithValidData_ReturnsUrl()
    {
        // Arrange
        var username = "alice";
        var blobId = $"test-image-{Guid.NewGuid()}.png";
        var testData = CreateTestImageData();
        var contentType = "image/png";

        // Act
        using var stream = new MemoryStream(testData);
        var url = await _blobStorage.StoreBlobAsync(username, blobId, stream, contentType);

        // Assert
        Assert.NotNull(url);
        Assert.Contains(username, url);
        Assert.Contains("media", url);
    }

    [Fact]
    public async Task GetBlobAsync_ExistingBlob_ReturnsContentAndType()
    {
        // Arrange
        var username = "alice";
        var blobId = $"test-{Guid.NewGuid()}.png";
        var testData = CreateTestImageData();
        var contentType = "image/png";

        using var uploadStream = new MemoryStream(testData);
        await _blobStorage.StoreBlobAsync(username, blobId, uploadStream, contentType);

        // Act
        var result = await _blobStorage.GetBlobAsync(username, blobId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(contentType, result.Value.ContentType);

        using var retrievedStream = new MemoryStream();
        await result.Value.Content.CopyToAsync(retrievedStream);
        var retrievedData = retrievedStream.ToArray();
        Assert.Equal(testData.Length, retrievedData.Length);
        Assert.Equal(testData, retrievedData);
    }

    [Fact]
    public async Task GetBlobAsync_NonExistentBlob_ReturnsNull()
    {
        // Arrange
        var username = "alice";
        var blobId = $"nonexistent-{Guid.NewGuid()}.png";

        // Act
        var result = await _blobStorage.GetBlobAsync(username, blobId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task BlobExistsAsync_ExistingBlob_ReturnsTrue()
    {
        // Arrange
        var username = "alice";
        var blobId = $"test-{Guid.NewGuid()}.png";
        var testData = CreateTestImageData();

        using var stream = new MemoryStream(testData);
        await _blobStorage.StoreBlobAsync(username, blobId, stream, "image/png");

        // Act
        var exists = await _blobStorage.BlobExistsAsync(username, blobId);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task BlobExistsAsync_NonExistentBlob_ReturnsFalse()
    {
        // Arrange
        var username = "alice";
        var blobId = $"nonexistent-{Guid.NewGuid()}.png";

        // Act
        var exists = await _blobStorage.BlobExistsAsync(username, blobId);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteBlobAsync_ExistingBlob_RemovesBlob()
    {
        // Arrange
        var username = "alice";
        var blobId = $"temp-{Guid.NewGuid()}.png";
        var testData = CreateTestImageData();

        using var stream = new MemoryStream(testData);
        await _blobStorage.StoreBlobAsync(username, blobId, stream, "image/png");

        // Verify it exists
        Assert.True(await _blobStorage.BlobExistsAsync(username, blobId));

        // Act
        await _blobStorage.DeleteBlobAsync(username, blobId);

        // Assert
        Assert.False(await _blobStorage.BlobExistsAsync(username, blobId));
        var retrieved = await _blobStorage.GetBlobAsync(username, blobId);
        Assert.Null(retrieved);
    }

    [Fact]
    public void BuildBlobUrl_WithUsernameAndBlobId_ReturnsCorrectUrl()
    {
        // Arrange
        var username = "alice";
        var blobId = "test-photo.jpg";

        // Act
        var url = _blobStorage.BuildBlobUrl(username, blobId);

        // Assert
        Assert.NotNull(url);
        Assert.StartsWith(BaseUrl, url);
        Assert.Contains("/users/alice/media/", url);
        Assert.EndsWith(blobId, url);
    }

    [Fact]
    public async Task StoreBlobAsync_MultipleBlobs_AllStoredAndRetrievable()
    {
        // Arrange
        var username = "alice";
        var blobIds = new List<string>
        {
            $"photo1-{Guid.NewGuid()}.png",
            $"photo2-{Guid.NewGuid()}.jpg",
            $"photo3-{Guid.NewGuid()}.webp"
        };
        var contentTypes = new[] { "image/png", "image/jpeg", "image/webp" };

        // Act - Store multiple blobs
        for (int i = 0; i < blobIds.Count; i++)
        {
            var testData = CreateTestImageData(i + 1);
            using var stream = new MemoryStream(testData);
            await _blobStorage.StoreBlobAsync(username, blobIds[i], stream, contentTypes[i]);
        }

        // Assert - Retrieve and verify each blob
        for (int i = 0; i < blobIds.Count; i++)
        {
            var retrieved = await _blobStorage.GetBlobAsync(username, blobIds[i]);
            Assert.NotNull(retrieved);
            Assert.Equal(contentTypes[i], retrieved.Value.ContentType);

            using var retrievedStream = new MemoryStream();
            await retrieved.Value.Content.CopyToAsync(retrievedStream);
            var expectedData = CreateTestImageData(i + 1);
            Assert.Equal(expectedData.Length, retrievedStream.Length);
        }
    }

    [Fact]
    public async Task StoreBlobAsync_DifferentUsers_BlobsAreIsolated()
    {
        // Arrange
        var blobId = "shared-name.png";
        var aliceData = CreateTestImageData(1);
        var bobData = CreateTestImageData(2);

        // Act - Store same blob ID for different users
        using (var aliceStream = new MemoryStream(aliceData))
        {
            await _blobStorage.StoreBlobAsync("alice", blobId, aliceStream, "image/png");
        }

        using (var bobStream = new MemoryStream(bobData))
        {
            await _blobStorage.StoreBlobAsync("bob", blobId, bobStream, "image/png");
        }

        // Assert - Each user gets their own blob
        var aliceBlob = await _blobStorage.GetBlobAsync("alice", blobId);
        var bobBlob = await _blobStorage.GetBlobAsync("bob", blobId);

        Assert.NotNull(aliceBlob);
        Assert.NotNull(bobBlob);

        using var aliceRetrieved = new MemoryStream();
        await aliceBlob.Value.Content.CopyToAsync(aliceRetrieved);

        using var bobRetrieved = new MemoryStream();
        await bobBlob.Value.Content.CopyToAsync(bobRetrieved);

        Assert.Equal(aliceData, aliceRetrieved.ToArray());
        Assert.Equal(bobData, bobRetrieved.ToArray());
        Assert.NotEqual(aliceRetrieved.ToArray(), bobRetrieved.ToArray());
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
