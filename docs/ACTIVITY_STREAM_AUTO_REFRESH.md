# Activity Stream Auto-Refresh Implementation

## Overview

The activity stream now supports automatic periodic polling with ID-based deduplication, and automatically refreshes when you post new content without requiring a full page refresh.

## Features Implemented

### 1. ID-Based Deduplication in CollectionLoader

The `CollectionLoader` component now tracks item IDs and prevents duplicates from appearing in the feed. This ensures that when new items are loaded (via polling or manual refresh), they won't duplicate existing items.

**Implementation Details:**
- Maintains a `HashSet<string>` of loaded item IDs
- Extracts IDs from items using reflection (looking for `Id` property)
- Filters out items with duplicate IDs during loading
- Clears the ID set when refreshing to allow fresh data

### 2. Automatic Polling (Enabled by Default)

The `ActivityFeed` component now has auto-refresh **enabled by default** with a 30-second polling interval.

**Default Behavior:**
```razor
<!-- Auto-refresh is now ON by default -->
<ActivityFeed CollectionUrl="@outboxUrl" />
```

**Customization:**
```razor
<!-- Adjust polling interval -->
<ActivityFeed CollectionUrl="@outboxUrl" 
              EnableAutoRefresh="true"
              AutoRefreshInterval="60" /> <!-- Poll every 60 seconds -->

<!-- Disable polling if needed -->
<ActivityFeed CollectionUrl="@outboxUrl" 
              EnableAutoRefresh="false" />
```

**Features:**
- Shows a refresh indicator (🔄) when auto-refresh is active
- Displays a notification banner when new posts are available
- Users can click to load new posts without interrupting their current view
- Tracks the latest activity ID to detect new content

### 3. Automatic Refresh on Post

When you post new content using `PostComposer`, all `ActivityFeed` instances automatically refresh to show the new post.

**How It Works:**
- New `ActivityStreamNotificationService` provides pub/sub pattern
- `PostComposer` publishes notification when posting succeeds
- `ActivityFeed` subscribes to notifications and auto-refreshes
- No manual refresh needed - your new post appears immediately

**Example Usage:**
```razor
@page "/timeline"

<div class="timeline-page">
    <!-- Composer for creating posts -->
    <PostComposer />
    
    <!-- Feed automatically updates when you post -->
    <ActivityFeed CollectionUrl="@outboxUrl" 
                  Title="Your Timeline" />
</div>
```

## Usage Examples

### Basic Timeline with Auto-Refresh
```razor
@page "/feed"
@using Broca.ActivityPub.Components

<div class="feed-container">
    <h2>Activity Feed</h2>
    
    <!-- Polls every 30 seconds, shows new post notifications -->
    <ActivityFeed CollectionUrl="@GetOutboxUrl()" 
                  Title="Latest Activity"
                  PageSize="20" />
</div>

@code {
    private Uri GetOutboxUrl() => new Uri("https://example.com/users/me/outbox");
}
```

### Custom Polling Interval
```razor
<!-- Check for updates every minute -->
<ActivityFeed CollectionUrl="@feedUrl" 
              AutoRefreshInterval="60"
              ShowNewPostsNotification="true" />
```

### Disable Auto-Scroll
```razor
<!-- Don't automatically load new posts, just show notification -->
<ActivityFeed CollectionUrl="@feedUrl" 
              AutoScrollToNew="false"
              ShowNewPostsNotification="true" />
```

### Complete Posting Experience
```razor
@page "/post"
@using Broca.ActivityPub.Components

<div class="compose-and-feed">
    <!-- Post composer -->
    <PostComposer Placeholder="What's happening?"
                  MaxLength="500"
                  OnPostCreated="HandlePostCreated" />
    
    <!-- Feed automatically refreshes when you post -->
    <ActivityFeed CollectionUrl="@myOutboxUrl" 
                  Title="My Posts"
                  EnableAutoRefresh="true"
                  AutoRefreshInterval="30" />
</div>

@code {
    private Uri myOutboxUrl => new Uri("https://example.com/users/me/outbox");
    
    private void HandlePostCreated(Activity activity)
    {
        // Optional: Handle post creation event
        // The feed will refresh automatically via the notification service
    }
}
```

## API Reference

### ActivityFeed Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `EnableAutoRefresh` | `bool` | `true` | Enable/disable automatic polling |
| `AutoRefreshInterval` | `int` | `30` | Polling interval in seconds |
| `ShowNewPostsNotification` | `bool` | `true` | Show notification banner for new posts |
| `AutoScrollToNew` | `bool` | `false` | Automatically load new posts vs showing notification |

### ActivityFeed Methods

```csharp
// Manually refresh the feed
await activityFeedRef.RefreshAsync();

// Enable/disable refresh programmatically
activityFeedRef.EnableRefresh();
activityFeedRef.DisableRefresh();
```

### PostComposer Events

```csharp
// Called when a post is successfully created
[Parameter]
public EventCallback<Activity> OnPostCreated { get; set; }
```

## Technical Details

### Deduplication Algorithm

1. When loading items, extract the `Id` property from each item
2. Check if the ID already exists in the `loadedItemIds` HashSet
3. Skip items with duplicate IDs
4. Add new items to both the items list and the ID set
5. On refresh, clear both collections and reload

### Notification Service

The `ActivityStreamNotificationService` is registered as a scoped service:
- Lives for the duration of the user's session/page
- Allows multiple components to communicate
- Handles exceptions in subscriber callbacks gracefully
- Cleans up subscriptions automatically via `IDisposable`

### Performance Considerations

- **Deduplication**: O(1) lookup using HashSet
- **Polling**: Configurable interval (default 30s) to balance freshness vs load
- **Notification**: Async pub/sub pattern doesn't block UI
- **Smart Refresh**: Only fetches first page to check for new items

## Migration Notes

If you have existing `ActivityFeed` instances with `EnableAutoRefresh="false"`, they will now have auto-refresh **enabled** by default. To maintain the old behavior:

```razor
<!-- Explicitly disable if you don't want polling -->
<ActivityFeed CollectionUrl="@url" 
              EnableAutoRefresh="false" />
```

## Benefits

1. **Better UX**: Users see updates without manual refresh
2. **Real-time Feel**: New posts appear automatically (30s default)
3. **No Duplicates**: ID-based deduplication prevents duplicate entries
4. **Instant Feedback**: Your own posts appear immediately after posting
5. **Configurable**: Can adjust or disable polling as needed
