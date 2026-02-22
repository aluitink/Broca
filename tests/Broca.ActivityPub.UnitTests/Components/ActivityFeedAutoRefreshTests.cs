using Xunit;
using Broca.ActivityPub.Components.Services;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.UnitTests.Components;

/// <summary>
/// Tests for ActivityStreamNotificationService and auto-refresh features
/// </summary>
public class ActivityStreamNotificationServiceTests
{
    [Fact]
    public async Task NotificationService_NotifiesSubscribers()
    {
        // Arrange
        var service = new ActivityStreamNotificationService();
        var callCount = 0;
        var activity = new Activity { Id = "test-activity", Type = new[] { "Create" } };

        var subscription = service.Subscribe(async (a) =>
        {
            callCount++;
            await Task.CompletedTask;
        });

        // Act
        await service.NotifyActivityPostedAsync(activity);

        // Assert
        Assert.Equal(1, callCount);

        // Cleanup
        subscription.Dispose();
    }

    [Fact]
    public async Task NotificationService_NotifiesMultipleSubscribers()
    {
        // Arrange
        var service = new ActivityStreamNotificationService();
        var callCount = 0;
        var activity = new Activity { Id = "test-activity", Type = new[] { "Create" } };

        var subscription1 = service.Subscribe(async (a) =>
        {
            callCount++;
            await Task.CompletedTask;
        });

        var subscription2 = service.Subscribe(async (a) =>
        {
            callCount++;
            await Task.CompletedTask;
        });

        // Act
        await service.NotifyActivityPostedAsync(activity);

        // Assert
        Assert.Equal(2, callCount);

        // Cleanup
        subscription1.Dispose();
        subscription2.Dispose();
    }

    [Fact]
    public async Task NotificationService_HandlesUnsubscribe()
    {
        // Arrange
        var service = new ActivityStreamNotificationService();
        var callCount = 0;
        var activity = new Activity { Id = "test-activity", Type = new[] { "Create" } };

        var subscription = service.Subscribe(async (a) =>
        {
            callCount++;
            await Task.CompletedTask;
        });

        // Act - First notification
        await service.NotifyActivityPostedAsync(activity);
        Assert.Equal(1, callCount);

        // Unsubscribe
        subscription.Dispose();

        // Second notification should not increment
        await service.NotifyActivityPostedAsync(activity);
        
        // Assert
        Assert.Equal(1, callCount); // Should still be 1, not 2
    }

    [Fact]
    public async Task NotificationService_HandlesExceptionsInSubscribers()
    {
        // Arrange
        var service = new ActivityStreamNotificationService();
        var successCallCount = 0;
        var activity = new Activity { Id = "test-activity", Type = new[] { "Create" } };

        // Subscriber that throws
        var subscription1 = service.Subscribe(async (a) =>
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("Test exception");
        });

        // Subscriber that should still be called despite the exception in subscription1
        var subscription2 = service.Subscribe(async (a) =>
        {
            successCallCount++;
            await Task.CompletedTask;
        });

        // Act
        await service.NotifyActivityPostedAsync(activity);

        // Assert - second subscriber should still have been called
        Assert.Equal(1, successCallCount);

        // Cleanup
        subscription1.Dispose();
        subscription2.Dispose();
    }

    [Fact]
    public async Task NotificationService_PassesCorrectActivityToSubscribers()
    {
        // Arrange
        var service = new ActivityStreamNotificationService();
        Activity? receivedActivity = null;
        var expectedActivity = new Activity 
        { 
            Id = "test-activity-123", 
            Type = new[] { "Create" }
        };

        var subscription = service.Subscribe(async (a) =>
        {
            receivedActivity = a;
            await Task.CompletedTask;
        });

        // Act
        await service.NotifyActivityPostedAsync(expectedActivity);

        // Assert
        Assert.NotNull(receivedActivity);
        Assert.Equal(expectedActivity.Id, receivedActivity.Id);
        Assert.Equal(expectedActivity.Type, receivedActivity.Type);

        // Cleanup
        subscription.Dispose();
    }
}
