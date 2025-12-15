# Broca.ActivityPub.Components

A flexible, extensible Blazor component library for building ActivityPub applications. This library provides pre-built components for common ActivityPub patterns while remaining framework-agnostic and highly customizable.

## Features

- 🎨 **Framework Agnostic**: No dependency on Fluent UI or any other UI framework
- 🔧 **Highly Customizable**: Template-based design allows complete control over rendering
- 🚀 **Performance Optimized**: Built-in virtualization support for large collections
- 📦 **Composable**: Small, focused components that work together
- 🎯 **Type Safe**: Strongly typed components with full IntelliSense support

## Architecture

### Design Patterns

The component library follows several key design patterns:

1. **Provider Pattern**: `ActivityPubClientProvider` manages client configuration
2. **Registry Pattern**: `IObjectRendererRegistry` handles type-to-renderer mapping
3. **Template Pattern**: All components expose `RenderFragment<T>` for customization
4. **Composition**: Small components compose into larger features

### Component Hierarchy

#### Foundation Layer
- **ServiceCollectionExtensions**: DI configuration
- **ActivityPubClientProvider**: Client configuration and cascading
- **IObjectRenderer**: Renderer interface and registry

#### Data Fetching Components
- **CollectionLoader<T>**: Generic collection loading with pagination
- **ActorBrowser**: Actor lookup via WebFinger/URI
- **ActorProfile**: Actor profile display with optional feed

#### Renderer Components
- **ObjectDisplay**: Polymorphic object renderer
- **ActivityRenderer**: Activity display with metadata
- **ActorRenderer**: Actor/profile cards
- **NoteRenderer**: Note/status rendering

#### Composite Components
- **ActivityFeed**: Activity stream with virtualization
- More to come...

## Getting Started

### Installation

```bash
dotnet add package Broca.ActivityPub.Components
```

### Basic Setup

1. **Configure Services** (Program.cs):

```csharp
using Broca.ActivityPub.Components.Extensions;

builder.Services.AddActivityPubComponents(options =>
{
    options.DefaultPageSize = 20;
    options.AutoFetchActors = true;
});
```

2. **Import Styles** (_Host.cshtml or index.html):

```html
<link href="_content/Broca.ActivityPub.Components/broca-activitypub-components.css" rel="stylesheet" />
```

3. **Add Imports** (_Imports.razor):

```razor
@using Broca.ActivityPub.Components
@using Broca.ActivityPub.Components.Renderers
```

## Usage Examples

### Simple Activity Feed

```razor
@page "/feed"

<ActivityFeed CollectionUrl="@(new Uri("https://mastodon.social/users/user/outbox"))" 
              Title="Public Timeline"
              PageSize="20" />
```

### Custom Activity Rendering

```razor
<ActivityFeed CollectionUrl="@outboxUrl">
    <ActivityTemplate Context="activity">
        <div class="custom-activity">
            <h4>@activity.Type</h4>
            <ObjectDisplay Object="@activity.Object?.FirstOrDefault()" />
            <small>@activity.Published</small>
        </div>
    </ActivityTemplate>
</ActivityFeed>
```

### Actor Browser with Custom UI

```razor
<ActorBrowser>
    <SearchTemplate Context="search">
        <div class="search-box">
            <input @bind="search.Query" placeholder="user@domain.com" />
            <button @onclick="search.Execute">Find Actor</button>
        </div>
    </SearchTemplate>
    <ProfileTemplate Context="actor">
        <ActorRenderer Actor="@actor" ShowDetails="true" />
    </ProfileTemplate>
</ActorBrowser>
```

### Actor Profile with Feed

```razor
<ActorProfile ActorHandle="user@mastodon.social" 
              ShowOutbox="true"
              OutboxPageSize="10" />
```

### Generic Collection Loader

```razor
<CollectionLoader TItem="Note" 
                 CollectionUrl="@collectionUrl"
                 UseVirtualization="true"
                 ItemSize="100">
    <ItemTemplate Context="note">
        <NoteRenderer Note="@note" />
    </ItemTemplate>
    <EmptyTemplate>
        <p>No notes found</p>
    </EmptyTemplate>
</CollectionLoader>
```

## Customization

### Custom Object Renderers

Create your own renderer by implementing `IObjectRenderer`:

```csharp
public class ArticleRenderer : ObjectRendererBase<Article>
{
    protected override RenderFragment Render(Article article)
    {
        return builder =>
        {
            builder.OpenElement(0, "article");
            builder.OpenElement(1, "h2");
            builder.AddContent(2, article.Name);
            builder.CloseElement();
            // ... more rendering
            builder.CloseElement();
        };
    }
}
```

Register it in Program.cs:

```csharp
builder.Services.AddObjectRenderer<Article, ArticleRenderer>();
```

Or create a Razor component:

```razor
@using KristofferStrube.ActivityStreams
@inherits ComponentBase

<article class="article">
    @if (Article != null)
    {
        <h2>@Article.Name</h2>
        <div class="content">@((MarkupString)Article.Content)</div>
    }
</article>

@code {
    [Parameter]
    public Article? Article { get; set; }
}
```

### Styling

All components use CSS classes with the `activitypub-` prefix. You can:

1. **Override default styles**: Target the provided classes
2. **Add custom classes**: Use the `CssClass` parameter
3. **Use CSS isolation**: Create component-specific styles

Example override:

```css
.activitypub-note {
    /* Your custom styles */
    border: 2px solid var(--primary-color);
    border-radius: 12px;
}

.activitypub-activity .type-badge {
    background-color: var(--badge-bg);
}
```

### Templates

Most components support multiple template parameters:

- **ItemTemplate**: Render individual items
- **LoadingTemplate**: Loading state
- **EmptyTemplate**: Empty state
- **ErrorTemplate**: Error display
- **ProfileTemplate**: Custom profile rendering
- **ActivityTemplate**: Custom activity rendering

## Components Reference

### ActivityPubClientProvider

Provides the ActivityPub client to child components via cascading parameter.

**Parameters:**
- `BaseUrl` (string?): Override base URL for the client
- `OnClientReady` (EventCallback<IActivityPubClient>): Callback when client is ready
- `ChildContent` (RenderFragment): Child content

### CollectionLoader<TItem>

Generic component for loading and displaying ActivityPub collections.

**Parameters:**
- `CollectionUrl` (Uri): Collection URL to load
- `PageSize` (int): Items per page (default: 20)
- `UseVirtualization` (bool): Enable virtualization (default: true)
- `ItemSize` (float): Item height in pixels for virtualization (default: 100)
- `ItemTemplate` (RenderFragment<TItem>): Item rendering template
- `LoadingTemplate`, `EmptyTemplate`, `ErrorTemplate`: State templates

**Methods:**
- `RefreshAsync()`: Reload the collection

### ActivityFeed

Specialized collection loader for activity streams.

**Parameters:**
- `CollectionUrl` (Uri): Activity collection URL
- `Title` (string?): Feed title
- `PageSize` (int): Items per page
- `ShowObjects` (bool): Show activity objects (default: true)
- `ShowRecipients` (bool): Show recipients (default: false)
- `ActivityTemplate` (RenderFragment<Activity>): Custom activity template

### ActorProfile

Loads and displays an actor profile.

**Parameters:**
- `ActorUri` (Uri?): Actor URI
- `ActorHandle` (string?): Actor handle (e.g., user@domain.com)
- `Actor` (Object?): Pre-loaded actor object
- `ShowDetails` (bool): Show detailed info (default: true)
- `ShowOutbox` (bool): Display outbox feed (default: false)
- `OutboxPageSize` (int): Outbox items to show
- `ProfileTemplate` (RenderFragment<Object>): Custom profile template

### ActorBrowser

Interactive actor search and display.

**Parameters:**
- `SearchTemplate` (RenderFragment<SearchContext>): Custom search UI
- `ProfileTemplate` (RenderFragment<Object>): Profile display
- `OnActorFound` (EventCallback<Object>): Actor found callback

### ObjectDisplay

Polymorphic renderer that dispatches to appropriate renderer based on object type.

**Parameters:**
- `Object` (object): Object to render
- `CustomRenderer` (RenderFragment<object>?): Override renderer
- `RenderEmpty` (RenderFragment?): Empty state

## Advanced Scenarios

### Infinite Scroll

```razor
<CollectionLoader TItem="Activity" 
                 CollectionUrl="@feedUrl"
                 UseVirtualization="true"
                 ItemSize="150"
                 OverscanCount="5">
    <ItemTemplate Context="activity">
        <ActivityRenderer Activity="@activity" />
    </ItemTemplate>
</CollectionLoader>
```

### Custom Loading States

```razor
<ActivityFeed CollectionUrl="@outboxUrl">
    <LoadingTemplate>
        <div class="custom-loader">
            <div class="spinner"></div>
            <p>Fetching latest posts...</p>
        </div>
    </LoadingTemplate>
    <EmptyTemplate>
        <div class="empty-state">
            <img src="/empty-inbox.svg" />
            <p>Your feed is empty</p>
        </div>
    </EmptyTemplate>
</ActivityFeed>
```

### Combining Components

```razor
<ActorBrowser>
    <ProfileTemplate Context="actor">
        <ActorRenderer Actor="@actor" />
        
        @if (actor is Person person && person.Outbox != null)
        {
            <ActivityFeed CollectionUrl="@person.Outbox" 
                         Title="Recent Posts"
                         PageSize="5" />
        }
    </ProfileTemplate>
</ActorBrowser>
```

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](../../CONTRIBUTING.md) for guidelines.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
