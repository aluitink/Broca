using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.FileSystem;
using Broca.ActivityPub.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.UnitTests;

public abstract class BlobStorageServiceTests
{
    protected const string BaseUrl = "https://test.example.com";

    protected abstract IBlobStorageService CreateService();

    [Fact]
    public async Task StoreBlobAsync_WithValidData_ReturnsUrl()
    {
        var service = CreateService();
        var blobId = $"test-image-{Guid.NewGuid()}.png";

        using var stream = new MemoryStream(CreateTestImageData());
        var url = await service.StoreBlobAsync("alice", blobId, stream, "image/png");

        Assert.NotNull(url);
        Assert.Contains("alice", url);
        Assert.Contains("media", url);
    }

    [Fact]
    public async Task StoreBlobAsync_UrlStartsWithBaseUrl()
    {
        var service = CreateService();

        using var stream = new MemoryStream(CreateTestImageData());
        var url = await service.StoreBlobAsync("alice", $"{Guid.NewGuid()}.png", stream, "image/png");

        Assert.StartsWith(BaseUrl, url);
    }

    [Fact]
    public async Task GetBlobAsync_ExistingBlob_ReturnsContentAndType()
    {
        var service = CreateService();
        var blobId = $"test-{Guid.NewGuid()}.png";
        var testData = CreateTestImageData();

        using var uploadStream = new MemoryStream(testData);
        await service.StoreBlobAsync("alice", blobId, uploadStream, "image/png");

        var result = await service.GetBlobAsync("alice", blobId);

        Assert.NotNull(result);
        Assert.Equal("image/png", result.Value.ContentType);

        using var retrievedStream = new MemoryStream();
        await result.Value.Content.CopyToAsync(retrievedStream);
        Assert.Equal(testData, retrievedStream.ToArray());
    }

    [Fact]
    public async Task GetBlobAsync_NonExistentBlob_ReturnsNull()
    {
        var service = CreateService();

        var result = await service.GetBlobAsync("alice", $"nonexistent-{Guid.NewGuid()}.png");

        Assert.Null(result);
    }

    [Fact]
    public async Task BlobExistsAsync_ExistingBlob_ReturnsTrue()
    {
        var service = CreateService();
        var blobId = $"test-{Guid.NewGuid()}.png";

        using var stream = new MemoryStream(CreateTestImageData());
        await service.StoreBlobAsync("alice", blobId, stream, "image/png");

        Assert.True(await service.BlobExistsAsync("alice", blobId));
    }

    [Fact]
    public async Task BlobExistsAsync_NonExistentBlob_ReturnsFalse()
    {
        var service = CreateService();

        Assert.False(await service.BlobExistsAsync("alice", $"nonexistent-{Guid.NewGuid()}.png"));
    }

    [Fact]
    public async Task DeleteBlobAsync_ExistingBlob_RemovesBlob()
    {
        var service = CreateService();
        var blobId = $"temp-{Guid.NewGuid()}.png";

        using var stream = new MemoryStream(CreateTestImageData());
        await service.StoreBlobAsync("alice", blobId, stream, "image/png");
        Assert.True(await service.BlobExistsAsync("alice", blobId));

        await service.DeleteBlobAsync("alice", blobId);

        Assert.False(await service.BlobExistsAsync("alice", blobId));
        Assert.Null(await service.GetBlobAsync("alice", blobId));
    }

    [Fact]
    public void BuildBlobUrl_WithUsernameAndBlobId_ReturnsCorrectUrl()
    {
        var service = CreateService();
        var url = service.BuildBlobUrl("alice", "test-photo.jpg");

        Assert.NotNull(url);
        Assert.StartsWith(BaseUrl, url);
        Assert.Contains("/users/alice/media/", url);
        Assert.EndsWith("test-photo.jpg", url);
    }

    [Fact]
    public async Task StoreBlobAsync_MultipleBlobs_AllStoredAndRetrievable()
    {
        var service = CreateService();
        var blobIds = new List<string>
        {
            $"photo1-{Guid.NewGuid()}.png",
            $"photo2-{Guid.NewGuid()}.jpg",
            $"photo3-{Guid.NewGuid()}.webp"
        };
        var contentTypes = new[] { "image/png", "image/jpeg", "image/webp" };

        for (var i = 0; i < blobIds.Count; i++)
        {
            using var stream = new MemoryStream(CreateTestImageData(i + 1));
            await service.StoreBlobAsync("alice", blobIds[i], stream, contentTypes[i]);
        }

        for (var i = 0; i < blobIds.Count; i++)
        {
            var retrieved = await service.GetBlobAsync("alice", blobIds[i]);
            Assert.NotNull(retrieved);
            Assert.Equal(contentTypes[i], retrieved.Value.ContentType);

            using var retrievedStream = new MemoryStream();
            await retrieved.Value.Content.CopyToAsync(retrievedStream);
            Assert.Equal(CreateTestImageData(i + 1).Length, retrievedStream.Length);
        }
    }

    [Fact]
    public async Task StoreBlobAsync_DifferentUsers_BlobsAreIsolated()
    {
        var service = CreateService();
        var blobId = $"shared-name-{Guid.NewGuid()}.png";
        var aliceData = CreateTestImageData(1);
        var bobData = CreateTestImageData(2);

        using (var aliceStream = new MemoryStream(aliceData))
            await service.StoreBlobAsync("alice", blobId, aliceStream, "image/png");

        using (var bobStream = new MemoryStream(bobData))
            await service.StoreBlobAsync("bob", blobId, bobStream, "image/png");

        var aliceBlob = await service.GetBlobAsync("alice", blobId);
        var bobBlob = await service.GetBlobAsync("bob", blobId);

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

    protected static byte[] CreateTestImageData(int seed = 1)
    {
        var data = new byte[1024];
        new Random(seed).NextBytes(data);
        data[0] = 0x89; data[1] = 0x50; data[2] = 0x4E; data[3] = 0x47;
        data[4] = 0x0D; data[5] = 0x0A; data[6] = 0x1A; data[7] = 0x0A;
        return data;
    }
}

public class InMemoryBlobStorageServiceTests : BlobStorageServiceTests
{
    protected override IBlobStorageService CreateService() =>
        new InMemoryBlobStorageService(
            Options.Create(new ActivityPubServerOptions { BaseUrl = BaseUrl }));
}

public class FileSystemBlobStorageServiceTests : BlobStorageServiceTests, IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "broca-blob-tests", Guid.NewGuid().ToString());

    protected override IBlobStorageService CreateService() =>
        new FileSystemBlobStorageService(
            Options.Create(new FileSystemPersistenceOptions { DataPath = _tempDir }),
            Options.Create(new ActivityPubServerOptions { BaseUrl = BaseUrl }),
            NullLogger<FileSystemBlobStorageService>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
