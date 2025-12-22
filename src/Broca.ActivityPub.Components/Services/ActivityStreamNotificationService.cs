using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Components.Services;

/// <summary>
/// Service for notifying components about new activities posted to the outbox.
/// This enables automatic feed updates without full page refreshes.
/// </summary>
public class ActivityStreamNotificationService
{
    private readonly List<Func<Activity, Task>> _subscribers = new();

    /// <summary>
    /// Subscribe to notifications when activities are posted.
    /// </summary>
    /// <param name="handler">Handler to invoke when an activity is posted</param>
    /// <returns>Disposable subscription that can be used to unsubscribe</returns>
    public IDisposable Subscribe(Func<Activity, Task> handler)
    {
        _subscribers.Add(handler);
        return new Subscription(() => _subscribers.Remove(handler));
    }

    /// <summary>
    /// Notify all subscribers that a new activity was posted.
    /// </summary>
    /// <param name="activity">The activity that was posted</param>
    public async Task NotifyActivityPostedAsync(Activity activity)
    {
        var tasks = _subscribers.Select(handler => SafeInvokeAsync(handler, activity));
        await Task.WhenAll(tasks);
    }

    private static async Task SafeInvokeAsync(Func<Activity, Task> handler, Activity activity)
    {
        try
        {
            await handler(activity);
        }
        catch
        {
            // Ignore exceptions from individual subscribers
            // to prevent one subscriber from breaking others
        }
    }

    private class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _unsubscribe();
                _disposed = true;
            }
        }
    }
}
