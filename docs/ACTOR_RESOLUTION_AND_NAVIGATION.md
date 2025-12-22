# Actor Resolution and Navigation Features

## Overview

This implementation adds comprehensive actor resolution and navigation features to the Broca ActivityPub client/components, allowing users to:

1. **Resolve Actor Information**: Automatically resolve actor URIs to display names and other details
2. **Navigate to Actor Profiles**: Click on actor personas or @mentions to view detailed actor profiles
3. **View Actor Collections**: Browse actor outboxes, relationships, and custom Broca collections
4. **Follow External Actors**: Follow/unfollow actors from other instances
5. **Open External Profiles**: Open actor profiles on their home instance

## Components Created

### 1. ActorResolutionService (`src/Broca.ActivityPub.Components/Services/ActorResolutionService.cs`)

A service for resolving and caching actor information from URIs.

**Key Features:**
- Resolves actor URIs to `ActorInfo` objects containing username, name, handle, icon, etc.
- Caches resolved actors for 15 minutes to reduce network requests
- Supports batch resolution of multiple actors in parallel
- Fallback handling when actor resolution fails

**Usage:**
```csharp
@inject ActorResolutionService ActorResolver

var actorInfo = await ActorResolver.ResolveActorAsync(actorLink);
// Access: actorInfo.PreferredUsername, actorInfo.Name, actorInfo.Handle, actorInfo.IconUrl
```

### 2. ActorView Component (`src/Broca.ActivityPub.Components/ActorView.razor`)

A comprehensive actor profile viewer with tabbed navigation.

**Features:**
- Displays actor profile information
- Tabbed interface for:
  - **Outbox**: Actor's posts/activities
  - **Following**: List of accounts the actor follows
  - **Followers**: List of the actor's followers
  - **Collections**: Custom Broca collections (if available)
- Follow/Unfollow button for external actors
- "Open Profile" button to view actor on their home instance
- Detects if actor is local or external

**Parameters:**
- `ActorUri`: Direct URI to actor
- `ActorHandle`: Actor handle (e.g., user@domain.com)
- `Username`: Local username
- `InitialTab`: Which tab to show initially
- `ShowFollowButton`: Enable/disable follow functionality
- `ShowOpenProfileButton`: Enable/disable external profile link

**Usage:**
```razor
<ActorView ActorUri="@actorUri" 
           InitialTab="ActorViewTab.Outbox"
           ShowFollowButton="true" />
```

### 3. Actor Page (`src/Broca.Web/Pages/Actor.razor`)

A Blazor page that wraps ActorView with query parameter support.

**URL Examples:**
- `/actor?id=https://mastodon.social/@user`
- `/actor?handle=user@mastodon.social`
- `/actor?username=localuser&tab=following`

### 4. Updated FluentNoteRenderer

Enhanced to use ActorResolutionService and add navigation:
- Resolves actor URIs on component initialization
- Displays resolved actor names, usernames, and avatars
- Click on actor persona navigates to `/actor?id=...` page
- Caches actor information to avoid repeated fetches

## Service Registration

The `ActorResolutionService` is automatically registered when you call `AddActivityPubComponents()`:

```csharp
services.AddActivityPubComponents();
```

This is done in `ServiceCollectionExtensions.cs`:

```csharp
services.TryAddScoped<ActorResolutionService>();
```

## Navigation Flow

1. **User Views Activity Feed**: Notes/posts show actor attributions
2. **User Clicks on Actor**: 
   - FluentNoteRenderer captures click
   - Navigates to `/actor?id={encodedActorUri}`
3. **Actor Page Loads**:
   - Parses query parameters
   - Renders ActorView component with appropriate parameters
4. **ActorView Loads Actor**:
   - Fetches actor from URI/handle/username
   - Loads custom collections if available
   - Checks follow status (TODO: needs full implementation)
   - Displays tabbed interface

## External vs Local Actor Detection

The ActorView component determines if an actor is external by comparing domains:

```csharp
private bool IsExternalActor
{
    get
    {
        // Compare actor's domain with client's actor domain
        if (Uri.TryCreate(currentActor.Id, ...) && Uri.TryCreate(Client.ActorId, ...))
        {
            return actorUri.Host != clientUri.Host;
        }
        return true;
    }
}
```

- **External Actors**: Show Follow and Open Profile buttons
- **Local Actors**: No Follow button (you can't follow yourself on same instance)

## Custom Collections Support

The ActorView component is **Broca custom collection aware**:

1. Fetches collection definitions via `ICollectionService`
2. Filters for public collections
3. Displays collections in the Collections tab
4. Allows viewing collection contents inline
5. Uses `CollectionLoader` component for pagination

## Follow/Unfollow Implementation

Basic follow functionality is implemented:

```csharp
private async Task FollowActorAsync()
{
    var builder = Client.CreateActivityBuilder();
    var followActivity = builder.CreateFollow(currentActor.Id);
    await Client.PostToOutboxAsync(followActivity);
    isFollowing = true;
}
```

**Note**: The unfollow implementation needs additional work to properly reference the original Follow activity for the Undo.

## Styling

Components include scoped CSS:
- `ActorView.razor.css`: Tabbed interface, action buttons, collections grid
- Responsive design with mobile support
- Integration with Fluent UI components

## Future Enhancements

1. **Follow Status Checking**: Implement querying current user's following collection
2. **@mention Detection**: Parse note content to detect and linkify @mentions
3. **Reply Threading**: Navigate to specific replies in actor's outbox
4. **Collection Filtering**: Allow filtering/searching within collections
5. **Activity Builder Enhancements**: Complete Undo Follow implementation
6. **Caching Strategy**: More sophisticated cache invalidation
7. **Offline Support**: Handle actor resolution failures gracefully

## Example Implementation

```razor
@page "/home"
@inject ActorResolutionService ActorResolver

<ActivityFeed CollectionUrl="@feedUrl" PageSize="20">
    <ActivityTemplate Context="activity">
        @* Activities automatically use FluentNoteRenderer *@
        @* which now resolves actors and enables navigation *@
    </ActivityTemplate>
</ActivityFeed>

@* Or directly use ActorView *@
<ActorView ActorHandle="user@mastodon.social" />
```

## Testing

To test the implementation:

1. **View Actor from Feed**:
   - Navigate to home/feed
   - Click on any actor's name/avatar in a post
   - Should navigate to actor profile page

2. **View External Actor**:
   - Enter URL: `/actor?id=https://mastodon.social/users/someuser`
   - Should show Follow and Open Profile buttons

3. **View Local Actor**:
   - Enter URL: `/actor?username=localuser`
   - Should not show Follow button

4. **Browse Collections**:
   - Navigate to actor with custom collections
   - Switch to Collections tab
   - Click on a collection to view its contents

## Integration Points

The implementation integrates with:
- `IActivityPubClient`: For fetching actors and posting activities
- `ICollectionService`: For loading custom collections
- `NavigationManager`: For page navigation
- `ObjectRendererRegistry`: For displaying collection items
- Existing Blazor components (ActivityFeed, CollectionLoader, etc.)
