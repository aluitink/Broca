using Microsoft.JSInterop;
using System.Text.Json;

namespace Broca.ActivityPub.Components.Services;

/// <summary>
/// Service for managing post drafts with automatic persistence to local storage.
/// </summary>
public class DraftManager : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly TimeSpan _autoSaveDelay = TimeSpan.FromSeconds(5);
    private readonly Dictionary<string, Timer> _autoSaveTimers = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private const string StoragePrefix = "broca_draft_";
    private const int MaxDrafts = 20;

    public DraftManager(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Saves a draft to local storage.
    /// </summary>
    /// <param name="draft">The draft to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveDraftAsync(PostDraft draft, CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            var key = GetStorageKey(draft.Context);
            var json = JsonSerializer.Serialize(draft);
            
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, key, json);
            
            draft.LastSaved = DateTime.UtcNow;
            draft.IsDirty = false;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>
    /// Schedules an auto-save for the draft after a delay.
    /// </summary>
    /// <param name="draft">The draft to auto-save.</param>
    public void ScheduleAutoSave(PostDraft draft)
    {
        var context = draft.Context;
        
        // Cancel existing timer for this context
        if (_autoSaveTimers.TryGetValue(context, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        // Create new timer
        var timer = new Timer(
            async _ => await SaveDraftAsync(draft),
            null,
            _autoSaveDelay,
            Timeout.InfiniteTimeSpan
        );

        _autoSaveTimers[context] = timer;
        draft.IsDirty = true;
    }

    /// <summary>
    /// Retrieves a draft from local storage.
    /// </summary>
    /// <param name="context">The draft context (e.g., "main", "reply:objectId").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The draft if found, otherwise null.</returns>
    public async Task<PostDraft?> GetDraftAsync(string context, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = GetStorageKey(context);
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, key);
            
            if (string.IsNullOrEmpty(json))
                return null;

            var draft = JsonSerializer.Deserialize<PostDraft>(json);
            if (draft != null)
            {
                draft.Context = context;
            }
            
            return draft;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes a draft from local storage.
    /// </summary>
    /// <param name="context">The draft context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeleteDraftAsync(string context, CancellationToken cancellationToken = default)
    {
        // Cancel any pending auto-save
        if (_autoSaveTimers.TryGetValue(context, out var timer))
        {
            timer.Dispose();
            _autoSaveTimers.Remove(context);
        }

        var key = GetStorageKey(context);
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, key);
    }

    /// <summary>
    /// Lists all available drafts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of draft contexts.</returns>
    public async Task<List<string>> ListDraftsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var length = await _jsRuntime.InvokeAsync<int>("localStorage.length", cancellationToken);
            var drafts = new List<string>();

            for (var i = 0; i < length; i++)
            {
                var key = await _jsRuntime.InvokeAsync<string?>("localStorage.key", cancellationToken, i);
                if (key?.StartsWith(StoragePrefix) == true)
                {
                    drafts.Add(key[StoragePrefix.Length..]);
                }
            }

            return drafts;
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Cleans up old drafts beyond the maximum limit.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CleanupOldDraftsAsync(CancellationToken cancellationToken = default)
    {
        var drafts = new List<(string context, DateTime lastSaved)>();

        var contexts = await ListDraftsAsync(cancellationToken);
        foreach (var context in contexts)
        {
            var draft = await GetDraftAsync(context, cancellationToken);
            if (draft != null)
            {
                drafts.Add((context, draft.LastSaved));
            }
        }

        // Sort by last saved, oldest first
        drafts.Sort((a, b) => a.lastSaved.CompareTo(b.lastSaved));

        // Remove oldest drafts if over limit
        var toRemove = drafts.Take(Math.Max(0, drafts.Count - MaxDrafts));
        foreach (var (context, _) in toRemove)
        {
            await DeleteDraftAsync(context, cancellationToken);
        }
    }

    /// <summary>
    /// Gets the storage key for a draft context.
    /// </summary>
    private static string GetStorageKey(string context) => $"{StoragePrefix}{context}";

    /// <summary>
    /// Creates a draft context string for a reply.
    /// </summary>
    /// <param name="inReplyToId">The object ID being replied to.</param>
    public static string GetReplyContext(string inReplyToId) => $"reply:{inReplyToId}";

    /// <summary>
    /// Gets the main composer context.
    /// </summary>
    public static string MainContext => "main";

    public ValueTask DisposeAsync()
    {
        foreach (var timer in _autoSaveTimers.Values)
        {
            timer.Dispose();
        }
        _autoSaveTimers.Clear();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Represents a saved post draft.
/// </summary>
public class PostDraft
{
    /// <summary>
    /// Gets or sets the draft context (e.g., "main", "reply:objectId").
    /// </summary>
    public string Context { get; set; } = DraftManager.MainContext;

    /// <summary>
    /// Gets or sets the post content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content warning.
    /// </summary>
    public string? ContentWarning { get; set; }

    /// <summary>
    /// Gets or sets whether a content warning is enabled.
    /// </summary>
    public bool HasContentWarning { get; set; }

    /// <summary>
    /// Gets or sets the visibility setting.
    /// </summary>
    public string Visibility { get; set; } = "public";

    /// <summary>
    /// Gets or sets the ID of the post being replied to, if any.
    /// </summary>
    public string? InReplyToId { get; set; }

    /// <summary>
    /// Gets or sets when the draft was created.
    /// </summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the draft was last saved.
    /// </summary>
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether the draft has unsaved changes.
    /// </summary>
    public bool IsDirty { get; set; }
}
