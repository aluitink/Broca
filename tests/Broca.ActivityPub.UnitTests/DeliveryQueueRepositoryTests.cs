using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.FileSystem;
using Broca.ActivityPub.Persistence.InMemory;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.UnitTests;

public abstract class DeliveryQueueRepositoryTests
{
    protected abstract IDeliveryQueueRepository CreateRepository();

    private static DeliveryQueueItem CreateQueueItem(string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Activity = new Create
        {
            Id = "https://example.com/activities/1",
            Type = new[] { "Create" }
        },
        InboxUrl = "https://remote.example/users/bob/inbox",
        SenderActorId = "https://example.com/users/alice",
        SenderUsername = "alice"
    };

    [Fact]
    public async Task EnqueueAsync_SingleItem_AppearsInPendingDeliveries()
    {
        var repo = CreateRepository();
        var item = CreateQueueItem();

        await repo.EnqueueAsync(item);

        var pending = await repo.GetPendingDeliveriesAsync();
        Assert.Single(pending);
    }

    [Fact]
    public async Task EnqueueAsync_NullItem_ThrowsArgumentNullException()
    {
        var repo = CreateRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.EnqueueAsync(null!));
    }

    [Fact]
    public async Task EnqueueBatchAsync_MultipleItems_AllAppearInPendingDeliveries()
    {
        var repo = CreateRepository();
        var items = Enumerable.Range(1, 3).Select(_ => CreateQueueItem()).ToList();

        await repo.EnqueueBatchAsync(items);

        var pending = await repo.GetPendingDeliveriesAsync();
        Assert.Equal(3, pending.Count());
    }

    [Fact]
    public async Task EnqueueBatchAsync_NullItems_ThrowsArgumentNullException()
    {
        var repo = CreateRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.EnqueueBatchAsync(null!));
    }

    [Fact]
    public async Task GetPendingDeliveriesAsync_EmptyQueue_ReturnsEmpty()
    {
        var repo = CreateRepository();

        var pending = await repo.GetPendingDeliveriesAsync();

        Assert.Empty(pending);
    }

    [Fact]
    public async Task GetPendingDeliveriesAsync_WithBatchSize_ReturnsLimitedResults()
    {
        var repo = CreateRepository();
        await repo.EnqueueBatchAsync(Enumerable.Range(1, 5).Select(_ => CreateQueueItem()));

        var pending = await repo.GetPendingDeliveriesAsync(batchSize: 3);

        Assert.Equal(3, pending.Count());
    }

    [Fact]
    public async Task MarkAsDeliveredAsync_PendingItem_NoLongerReturnedAsPending()
    {
        var repo = CreateRepository();
        var item = CreateQueueItem();
        await repo.EnqueueAsync(item);
        var pending = await repo.GetPendingDeliveriesAsync();

        await repo.MarkAsDeliveredAsync(pending.First().Id);

        var pendingAfter = await repo.GetPendingDeliveriesAsync();
        Assert.Empty(pendingAfter);
    }

    [Fact]
    public async Task MarkAsFailedAsync_ItemUnderMaxRetries_IsNotImmediatelyAvailableForRetry()
    {
        var repo = CreateRepository();
        var item = CreateQueueItem();
        await repo.EnqueueAsync(item);
        var pending = await repo.GetPendingDeliveriesAsync();

        await repo.MarkAsFailedAsync(pending.First().Id, "connection refused");

        var pendingAfter = await repo.GetPendingDeliveriesAsync();
        Assert.Empty(pendingAfter);
    }

    [Fact]
    public async Task MarkAsFailedAsync_ItemAtMaxRetries_IsNotReturnedAsPending()
    {
        var repo = CreateRepository();
        var item = CreateQueueItem();
        item.MaxRetries = 1;
        await repo.EnqueueAsync(item);
        var pending = await repo.GetPendingDeliveriesAsync();

        await repo.MarkAsFailedAsync(pending.First().Id, "permanently failed");

        var pendingAfter = await repo.GetPendingDeliveriesAsync();
        Assert.Empty(pendingAfter);
    }

    [Fact]
    public async Task GetStatisticsAsync_AfterEnqueue_HasCorrectPendingCount()
    {
        var repo = CreateRepository();
        await repo.EnqueueBatchAsync(Enumerable.Range(1, 3).Select(_ => CreateQueueItem()));

        var stats = await repo.GetStatisticsAsync();

        Assert.Equal(3, stats.PendingCount);
    }

    [Fact]
    public async Task GetStatisticsAsync_EmptyQueue_ReturnsZeroCounts()
    {
        var repo = CreateRepository();

        var stats = await repo.GetStatisticsAsync();

        Assert.Equal(0, stats.PendingCount);
        Assert.Equal(0, stats.DeliveredCount);
        Assert.Equal(0, stats.FailedCount);
        Assert.Equal(0, stats.DeadCount);
    }

    [Fact]
    public async Task GetStatisticsAsync_AfterDelivery_DeliveredCountIncremented()
    {
        var repo = CreateRepository();
        var item = CreateQueueItem();
        await repo.EnqueueAsync(item);
        var pending = await repo.GetPendingDeliveriesAsync();
        await repo.MarkAsDeliveredAsync(pending.First().Id);

        var stats = await repo.GetStatisticsAsync();

        Assert.Equal(1, stats.DeliveredCount);
        Assert.Equal(0, stats.PendingCount);
    }

    [Fact]
    public async Task GetAllForDiagnosticsAsync_EmptyQueue_ReturnsEmpty()
    {
        var repo = CreateRepository();

        var all = await repo.GetAllForDiagnosticsAsync();

        Assert.Empty(all);
    }

    [Fact]
    public async Task GetAllForDiagnosticsAsync_AfterEnqueue_ReturnsEnqueuedItem()
    {
        var repo = CreateRepository();
        var item = CreateQueueItem();
        await repo.EnqueueAsync(item);

        var all = await repo.GetAllForDiagnosticsAsync();

        Assert.Single(all);
    }

    [Fact]
    public async Task CleanupOldItemsAsync_RecentDeliveredItems_RetainsItems()
    {
        var repo = CreateRepository();
        var item = CreateQueueItem();
        await repo.EnqueueAsync(item);
        var pending = await repo.GetPendingDeliveriesAsync();
        await repo.MarkAsDeliveredAsync(pending.First().Id);

        await repo.CleanupOldItemsAsync(TimeSpan.FromDays(1));

        var stats = await repo.GetStatisticsAsync();
        Assert.Equal(1, stats.DeliveredCount);
    }

    [Fact]
    public async Task GetStatisticsAsync_AfterFailure_FailedCountIncremented()
    {
        var repo = CreateRepository();
        var item = CreateQueueItem();
        await repo.EnqueueAsync(item);
        var pending = await repo.GetPendingDeliveriesAsync();
        await repo.MarkAsFailedAsync(pending.First().Id, "timeout");

        var stats = await repo.GetStatisticsAsync();

        Assert.Equal(1, stats.FailedCount);
    }

    [Fact]
    public async Task GetStatisticsAsync_ItemExceedingMaxRetries_DeadCountIncremented()
    {
        var repo = CreateRepository();
        var item = CreateQueueItem();
        item.MaxRetries = 1;
        await repo.EnqueueAsync(item);
        var pending = await repo.GetPendingDeliveriesAsync();
        await repo.MarkAsFailedAsync(pending.First().Id, "permanently failed");

        var stats = await repo.GetStatisticsAsync();

        Assert.Equal(1, stats.DeadCount);
        Assert.Equal(0, stats.FailedCount);
    }

    [Fact]
    public async Task GetStatisticsAsync_AfterDelivery_OldestPendingItemReflectsRemainingItems()
    {
        var repo = CreateRepository();
        var item1 = CreateQueueItem();
        var item2 = CreateQueueItem();
        await repo.EnqueueAsync(item1);
        await repo.EnqueueAsync(item2);
        var pending = await repo.GetPendingDeliveriesAsync(batchSize: 1);
        await repo.MarkAsDeliveredAsync(pending.First().Id);

        var stats = await repo.GetStatisticsAsync();

        Assert.Equal(1, stats.PendingCount);
    }
}

public class InMemoryDeliveryQueueRepositoryTests : DeliveryQueueRepositoryTests
{
    protected override IDeliveryQueueRepository CreateRepository() => new InMemoryDeliveryQueueRepository();
}

public class FileSystemDeliveryQueueRepositoryTests : DeliveryQueueRepositoryTests, IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "broca-queue-tests", Guid.NewGuid().ToString());

    protected override IDeliveryQueueRepository CreateRepository() =>
        new FileSystemDeliveryQueueRepository(
            Options.Create(new FileSystemPersistenceOptions { DataPath = _tempDir }),
            NullLogger<FileSystemDeliveryQueueRepository>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
