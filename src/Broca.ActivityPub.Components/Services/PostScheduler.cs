using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Components.Services;

public class ScheduledPost
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public string Visibility { get; set; } = "public";
    public string? ContentWarning { get; set; }
    public bool HasContentWarning { get; set; }
    public string? InReplyToId { get; set; }
    public DateTime ScheduledFor { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ScheduledPostStatus Status { get; set; } = ScheduledPostStatus.Pending;
    public string? ErrorMessage { get; set; }
    public List<string> AttachmentUrls { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public enum ScheduledPostStatus
{
    Pending,
    Published,
    Failed,
    Cancelled
}

public class PostScheduler
{
    private readonly Dictionary<string, ScheduledPost> _scheduledPosts = new();
    private readonly System.Threading.Timer? _checkTimer;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public event Func<ScheduledPost, Task>? OnPostDue;
    public event Func<ScheduledPost, Task>? OnPostStatusChanged;

    public PostScheduler()
    {
        _checkTimer = new System.Threading.Timer(
            async _ => await CheckDuePostsAsync(),
            null,
            _checkInterval,
            _checkInterval);
    }

    /// <summary>
    /// Schedules a post for future publication.
    /// </summary>
    public Task<ScheduledPost> SchedulePostAsync(
        string content,
        DateTime scheduledFor,
        string visibility = "public",
        string? contentWarning = null,
        string? inReplyToId = null,
        List<string>? attachmentUrls = null)
    {
        if (scheduledFor <= DateTime.UtcNow)
        {
            throw new ArgumentException("Scheduled time must be in the future", nameof(scheduledFor));
        }

        var scheduledPost = new ScheduledPost
        {
            Content = content,
            ScheduledFor = scheduledFor,
            Visibility = visibility,
            ContentWarning = contentWarning,
            HasContentWarning = !string.IsNullOrEmpty(contentWarning),
            InReplyToId = inReplyToId,
            AttachmentUrls = attachmentUrls ?? new List<string>()
        };

        _scheduledPosts[scheduledPost.Id] = scheduledPost;
        return Task.FromResult(scheduledPost);
    }

    /// <summary>
    /// Gets all scheduled posts.
    /// </summary>
    public Task<List<ScheduledPost>> GetScheduledPostsAsync()
    {
        return Task.FromResult(_scheduledPosts.Values
            .Where(p => p.Status == ScheduledPostStatus.Pending)
            .OrderBy(p => p.ScheduledFor)
            .ToList());
    }

    /// <summary>
    /// Gets a scheduled post by ID.
    /// </summary>
    public Task<ScheduledPost?> GetScheduledPostAsync(string id)
    {
        _scheduledPosts.TryGetValue(id, out var post);
        return Task.FromResult(post);
    }

    /// <summary>
    /// Cancels a scheduled post.
    /// </summary>
    public Task<bool> CancelScheduledPostAsync(string id)
    {
        if (_scheduledPosts.TryGetValue(id, out var post) && post.Status == ScheduledPostStatus.Pending)
        {
            post.Status = ScheduledPostStatus.Cancelled;
            NotifyStatusChanged(post);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <summary>
    /// Updates a scheduled post.
    /// </summary>
    public Task<bool> UpdateScheduledPostAsync(string id, Action<ScheduledPost> updateAction)
    {
        if (_scheduledPosts.TryGetValue(id, out var post) && post.Status == ScheduledPostStatus.Pending)
        {
            updateAction(post);
            NotifyStatusChanged(post);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <summary>
    /// Deletes a scheduled post.
    /// </summary>
    public Task<bool> DeleteScheduledPostAsync(string id)
    {
        if (_scheduledPosts.Remove(id))
        {
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <summary>
    /// Marks a scheduled post as published.
    /// </summary>
    public Task MarkAsPublishedAsync(string id)
    {
        if (_scheduledPosts.TryGetValue(id, out var post))
        {
            post.Status = ScheduledPostStatus.Published;
            NotifyStatusChanged(post);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Marks a scheduled post as failed.
    /// </summary>
    public Task MarkAsFailedAsync(string id, string errorMessage)
    {
        if (_scheduledPosts.TryGetValue(id, out var post))
        {
            post.Status = ScheduledPostStatus.Failed;
            post.ErrorMessage = errorMessage;
            NotifyStatusChanged(post);
        }
        return Task.CompletedTask;
    }

    private async Task CheckDuePostsAsync()
    {
        var now = DateTime.UtcNow;
        var duePosts = _scheduledPosts.Values
            .Where(p => p.Status == ScheduledPostStatus.Pending && p.ScheduledFor <= now)
            .ToList();

        foreach (var post in duePosts)
        {
            if (OnPostDue != null)
            {
                try
                {
                    await OnPostDue.Invoke(post);
                }
                catch (Exception ex)
                {
                    post.Status = ScheduledPostStatus.Failed;
                    post.ErrorMessage = ex.Message;
                    NotifyStatusChanged(post);
                }
            }
        }
    }

    private void NotifyStatusChanged(ScheduledPost post)
    {
        if (OnPostStatusChanged != null)
        {
            _ = Task.Run(() => OnPostStatusChanged.Invoke(post));
        }
    }

    public void Dispose()
    {
        _checkTimer?.Dispose();
    }
}
