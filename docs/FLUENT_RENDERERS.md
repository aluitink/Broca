# Fluent UI Renderers Implementation

## Overview

This implementation provides a complete set of Fluent UI-based renderers for ActivityStreams objects in the Broca.Web project. These renderers replace the default component library renderers with custom Fluent UI components for a consistent, modern UI experience.

## Architecture

The renderer system uses a **proxy pattern** where each renderer implements `IObjectRenderer` through the `ObjectRendererBase<T>` base class. The renderers are registered in the `FluentRendererExtensions.cs` file and are automatically used by the `ObjectDisplay` component when rendering ActivityStreams objects.

### Registration Flow

1. **Program.cs** gets the `IObjectRendererRegistry` from DI
2. Calls `RegisterFluentRenderers()` extension method
3. Extension method registers proxy classes for each ActivityStreams type
4. Proxy classes create RenderFragments that instantiate the actual Fluent UI renderer components

## Implemented Renderers

### 1. FluentNoteRenderer

**Purpose**: Renders ActivityPub Note objects (short-form posts)

**Features**:
- Actor attribution with icon
- Rich HTML content rendering
- Attachment display (images, documents)
- Action buttons (Reply, Boost, Like)
- Timestamp with relative time display
- Responsive card layout

**Parameters**:
- `Note` - The Note object to render
- `ShowActor` - Display actor attribution (default: true)
- `ShowActions` - Display action buttons (default: true)
- `CssClass` - Additional CSS classes
- `AdditionalContent` - Custom content template
- `OnReply`, `OnBoost`, `OnLike` - Event callbacks for actions

**Styling**: FluentNoteRenderer.razor.css

### 2. FluentArticleRenderer

**Purpose**: Renders ActivityPub Article objects (long-form content)

**Features**:
- Featured image display
- Article title and metadata
- Author attribution
- Read time estimation
- Summary/excerpt display
- Rich content rendering
- Tag display with badges
- Professional article layout

**Parameters**:
- `Article` - The Article object to render
- `ShowAuthor` - Display author info (default: true)
- `ShowSummary` - Display article summary (default: true)
- `ShowFeaturedImage` - Display featured image (default: true)
- `ShowTags` - Display article tags (default: true)
- `ShowReadTime` - Display estimated read time (default: true)
- `CssClass` - Additional CSS classes
- `AdditionalContent` - Custom content template

**Styling**: FluentArticleRenderer.razor.css

### 3. FluentActorRenderer

**Purpose**: Renders Actor/Person profile cards

**Features**:
- Avatar display with FluentPersona
- Name and username display
- Profile summary/bio
- Join date display
- Profile link
- Follow button (optional)
- Statistics display (following/followers)
- Compact profile card design

**Parameters**:
- `Actor` - The Actor object to render
- `ShowDetails` - Show detailed information (default: true)
- `ShowStats` - Show follower statistics (default: true)
- `ShowFollowButton` - Display follow button (default: false)
- `CssClass` - Additional CSS classes
- `AdditionalContent` - Custom content template
- `OnFollow` - Event callback for follow action

**Styling**: FluentActorRenderer.razor.css

### 4. FluentActivityRenderer

**Purpose**: Renders Activity objects (Create, Like, Follow, etc.)

**Features**:
- Activity type badge with emoji
- Actor information
- Timestamp display
- Activity object rendering (recursive)
- Recipient display
- Activity type-specific icons
- Clean activity feed layout

**Parameters**:
- `Activity` - The Activity object to render
- `ShowObject` - Display the activity's object (default: true)
- `ShowRecipients` - Display activity recipients (default: false)
- `CssClass` - Additional CSS classes
- `ObjectRenderer` - Custom object renderer
- `AdditionalContent` - Custom content template

**Styling**: FluentActivityRenderer.razor.css

### 5. FluentImageRenderer

**Purpose**: Renders Image attachments

**Features**:
- Responsive image display
- Lazy loading
- Compact mode for thumbnails
- Image metadata display
- File type badge
- Open action button
- Alt text support

**Parameters**:
- `Image` - The Image object to render
- `ShowMetadata` - Display image metadata (default: true)
- `ShowActions` - Display action buttons (default: true)
- `IsCompact` - Render in compact mode (default: false)
- `CompactMaxWidth` - Maximum width in compact mode (default: "200px")
- `CssClass` - Additional CSS classes
- `AdditionalContent` - Custom content template

### 6. FluentVideoRenderer

**Purpose**: Renders Video attachments

**Features**:
- Native HTML5 video player
- Video metadata display
- Duration display
- File type badge
- Download button
- Fallback for unavailable videos

**Parameters**:
- `Video` - The Video object to render
- `ShowMetadata` - Display video metadata (default: true)
- `ShowActions` - Display action buttons (default: true)
- `CssClass` - Additional CSS classes
- `AdditionalContent` - Custom content template

### 7. FluentDocumentRenderer

**Purpose**: Renders Document attachments (PDFs, Office files, etc.)

**Features**:
- Document type icons (PDF, Word, Excel, etc.)
- File type badges
- Compact and full display modes
- Open and download actions
- Document metadata display

**Parameters**:
- `Document` - The Document object to render
- `ShowMetadata` - Display document metadata (default: true)
- `ShowActions` - Display action buttons (default: true)
- `IsCompact` - Render in compact mode (default: false)
- `CssClass` - Additional CSS classes
- `AdditionalContent` - Custom content template

### 8. FluentAddContentButtonRenderer

**Purpose**: Renders a modern Floating Action Button (FAB) for creating new posts

**Features**:
- Circular FAB with "+" icon in Fluent design
- Fixed positioning in bottom right corner
- Floats above all content with high z-index
- Smooth dialog animations (fade-in overlay, slide-up dialog)
- Integrated with PostComposer using Fluent renderer
- Fluent UI components for mentions, tags, and attachments
- Responsive design for mobile and desktop

**Parameters**:
- `Context` - The AddContentButton component instance
- Position customization via Context properties:
  - `BottomPosition` - Distance from bottom (default: 24px)
  - `RightPosition` - Distance from right (default: 24px)

**Usage**:
```razor
<AddContentButton 
    ButtonTemplate="FluentAddContentButtonRenderer.Template"
    OnPostCreated="HandlePostCreated" />
```

**Styling**: FluentAddContentButtonRenderer.razor.css

## Type Mappings

The following ActivityStreams types and components are mapped to renderers:

```csharp
Note → FluentNoteRenderer
Article → FluentArticleRenderer
Person → FluentActorRenderer
Actor → FluentActorRenderer
Activity → FluentActivityRenderer
Image → FluentImageRenderer
Video → FluentVideoRenderer
Document → FluentDocumentRenderer
AddContentButton (component) → FluentAddContentButtonRenderer
PostComposer (component) → FluentPostComposerRenderer
```

## Usage

### Basic Usage

The renderers are automatically used when you use the `ObjectDisplay` component:

```razor
@using Broca.ActivityPub.Components

<ObjectDisplay Object="@myNote" />
```

### Custom Parameters

You can pass custom parameters by using the renderers directly:

```razor
<FluentNoteRenderer 
    Note="@myNote"
    ShowActions="true"
    OnReply="@HandleReply"
    OnLike="@HandleLike" />
```

### Custom Templates

All renderers support custom content templates:

```razor
<FluentArticleRenderer Article="@article">
    <AdditionalContent Context="article">
        <div class="custom-footer">
            Custom content here
        </div>
    </AdditionalContent>
</FluentArticleRenderer>
```

## Extending the Renderers

### Adding a New Renderer

1. Create a new `.razor` file in `src/Broca.Web/Renderers/`
2. Implement the renderer component with Fluent UI components
3. Create a proxy class in `FluentRendererExtensions.cs`:

```csharp
internal class FluentMyTypeRendererProxy : ObjectRendererBase<MyType>
{
    protected override RenderFragment Render(MyType obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentMyTypeRenderer>(0);
            builder.AddAttribute(1, "MyType", obj);
            builder.CloseComponent();
        };
    }
}
```

4. Register it in `RegisterFluentRenderers()`:

```csharp
registry.RegisterRenderer(typeof(MyType), new FluentMyTypeRendererProxy());
```

### Customizing Existing Renderers

You can override the registered renderers at runtime:

```csharp
var registry = serviceProvider.GetRequiredService<IObjectRendererRegistry>();
registry.RegisterRenderer(typeof(Note), new MyCustomNoteRenderer());
```

## Design Decisions

### Why Proxy Pattern?

The proxy pattern allows us to:
- Keep renderer components as regular Blazor components (not IObjectRenderer implementations)
- Use Razor syntax and parameters naturally
- Maintain type safety
- Easily swap renderers at runtime

### Component Parameters vs. IObjectRenderer

We chose to use component parameters because:
- Better IntelliSense support
- Easier to understand and use
- More flexible (event callbacks, templates)
- Follows Blazor best practices

### Fluent UI Component Selection

We use the following Fluent UI components:
- `FluentCard` - Container for content
- `FluentButton` - Actions and interactions
- `FluentIcon` - Icons throughout the UI
- `FluentLabel` - Text display
- `FluentBadge` - Tags and metadata
- `FluentStack` - Layout management
- `FluentDivider` - Visual separation
- `FluentPersona` - Profile display

## Styling

Each renderer has an associated `.razor.css` file with scoped styles. The styles follow Fluent UI design tokens:

- `var(--neutral-layer-2)` - Secondary background
- `var(--neutral-foreground-rest)` - Default text color
- `var(--accent-fill-rest)` - Accent color
- Standard spacing units (4px, 8px, 12px, 16px, 24px)

## Testing

To test the renderers:

1. Build the project: `dotnet build src/Broca.Web/Broca.Web.csproj`
2. Run the web application
3. Navigate to pages that display ActivityStreams objects
4. Verify that objects are rendered with Fluent UI components

## Future Enhancements

Potential improvements:

1. **Additional Renderers**: Collection, Question, Event, Place renderers
2. **Theming Support**: Dark/light mode switching
3. **Accessibility**: Enhanced ARIA labels and keyboard navigation
4. **Performance**: Virtual scrolling for large collections
5. **Customization**: Theme overrides and style props
6. **Internationalization**: Multi-language support
7. **Actions**: Implement actual follow/like/boost functionality
8. **Media Gallery**: Enhanced image/video gallery views

## Troubleshooting

### Renderer Not Being Used

Check that:
1. `RegisterFluentRenderers()` is called in Program.cs
2. The type exactly matches the ActivityStreams type
3. The object is being passed through `ObjectDisplay` component

### Styling Not Applied

Verify:
1. CSS files are included in the project
2. Scoped CSS is enabled in the project
3. Build includes the CSS files in wwwroot

### Icons Not Showing

Ensure:
1. `Microsoft.FluentUI.AspNetCore.Components` package is installed
2. Fluent UI services are registered in DI
3. Icon imports are correct: `@using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons`

## Related Files

- `/src/Broca.Web/Renderers/FluentRendererExtensions.cs` - Registration
- `/src/Broca.Web/Program.cs` - Initialization
- `/src/Broca.ActivityPub.Components/Services/IObjectRenderer.cs` - Interface
- `/src/Broca.ActivityPub.Components/Services/ObjectRendererRegistry.cs` - Registry
- `/src/Broca.ActivityPub.Components/ObjectDisplay.razor` - Consumer
