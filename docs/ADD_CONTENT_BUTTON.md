# AddContentButton Component - Usage Examples

The `AddContentButton` component provides a floating action button (FAB) for creating posts with rich features including media attachments, mentions, tags, and privacy controls.

## Basic Usage

### Simple Floating Button

```razor
@page "/home"
@using Broca.ActivityPub.Components

<!-- Floating button with default settings -->
<AddContentButton OnPostCreated="HandlePostCreated" />

@code {
    private void HandlePostCreated(Activity activity)
    {
        // Handle the created post
        Console.WriteLine($"Post created: {activity.Id}");
    }
}
```

### Custom Positioning

```razor
<!-- Position 40px from bottom and 40px from right -->
<AddContentButton 
    BottomPosition="40"
    RightPosition="40"
    OnPostCreated="HandlePostCreated" />
```

### Custom Icon and Tooltip

```razor
<!-- Use a different icon and tooltip -->
<AddContentButton 
    ButtonIcon="➕"
    ButtonTitle="Compose new message"
    OnPostCreated="HandlePostCreated" />
```

## Advanced Usage

### Different Character Limits

```razor
<!-- Allow longer posts -->
<AddContentButton 
    MaxLength="1000"
    Placeholder="Share your thoughts..."
    OnPostCreated="HandlePostCreated" />
```

### Open Dialog on Page Load

```razor
<!-- Automatically show the composer dialog when page loads -->
<AddContentButton 
    ShowDialogOnInit="true"
    OnPostCreated="HandlePostCreated" />
```

### Hide the Button (Dialog Only Mode)

```razor
<!-- Use with a custom trigger button -->
<button @onclick="OpenComposer">Write a Post</button>

<AddContentButton 
    @ref="composerRef"
    HideButton="true"
    OnPostCreated="HandlePostCreated" />

@code {
    private AddContentButton? composerRef;
    
    private void OpenComposer()
    {
        composerRef?.Open();
    }
}
```

### Custom File Types

```razor
<!-- Accept only images -->
<AddContentButton 
    AcceptedFileTypes="image/png,image/jpeg,image/gif"
    OnPostCreated="HandlePostCreated" />

<!-- Accept images and PDFs -->
<AddContentButton 
    AcceptedFileTypes="image/*,application/pdf"
    OnPostCreated="HandlePostCreated" />
```

## Integration Patterns

### With Activity Feed

```razor
@page "/timeline"

<!-- Feed of recent posts -->
<ActivityFeed CollectionUrl="@outboxUrl" @ref="feedRef" />

<!-- Floating button to create new posts -->
<AddContentButton OnPostCreated="HandlePostCreated" />

@code {
    private Uri outboxUrl = new Uri("https://example.com/users/me/outbox");
    private ActivityFeed? feedRef;
    
    private async Task HandlePostCreated(Activity activity)
    {
        // Refresh the feed to show the new post
        await feedRef?.RefreshAsync();
    }
}
```

### With Navigation

```razor
<AddContentButton OnPostCreated="NavigateToPost" />

@code {
    [Inject]
    private NavigationManager Navigation { get; set; } = null!;
    
    private void NavigateToPost(Activity activity)
    {
        var postId = activity.Id?.ToString() ?? "";
        Navigation.NavigateTo($"/posts/{postId}");
    }
}
```

### With State Management

```razor
<AddContentButton OnPostCreated="UpdateState" />

@if (showSuccess)
{
    <div class="success-toast">Post created successfully!</div>
}

@code {
    private bool showSuccess = false;
    
    private async Task UpdateState(Activity activity)
    {
        showSuccess = true;
        StateHasChanged();
        
        // Hide after 3 seconds
        await Task.Delay(3000);
        showSuccess = false;
        StateHasChanged();
    }
}
```

## Features

### Media Attachments
- Upload images and videos
- Preview thumbnails before posting
- Remove attachments individually
- Supports multiple files

### Mentions (@username)
- Add mentions to notify other users
- Format: `user@domain.com`
- Press Enter to add a mention
- Remove mentions individually

### Hashtags (#tags)
- Add hashtags to categorize posts
- Input without the `#` symbol
- Press Enter to add a tag
- Remove tags individually

### Privacy Settings
- **Public**: Visible to everyone
- **Unlisted**: Public but not in timelines
- **Followers**: Only your followers can see
- **Direct**: Only mentioned users can see

## Styling

### CSS Classes

The component uses these CSS classes that you can override:

```css
.activitypub-add-content-wrapper { }
.add-content-fab { }
.add-content-overlay { }
.add-content-dialog { }
.dialog-header { }
.dialog-body { }
.attachment-preview-section { }
.mentions-section { }
.tags-section { }
```

### Custom Styles Example

```css
/* Make the FAB larger and change color */
.add-content-fab {
    width: 70px;
    height: 70px;
    background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
}

/* Customize dialog appearance */
.add-content-dialog {
    max-width: 700px;
    border-radius: 20px;
}
```

### Dark Mode Support

The component automatically adapts to dark mode using the `prefers-color-scheme` media query.

## Complete Example

```razor
@page "/social"
@using Broca.ActivityPub.Components
@inject IActivityPubClient Client

<h1>Social Feed</h1>

@if (posts.Any())
{
    <div class="posts">
        @foreach (var post in posts)
        {
            <div class="post-card">
                <p>@post.Content</p>
                <small>@post.CreatedAt.ToString("g")</small>
            </div>
        }
    </div>
}

<AddContentButton 
    ButtonIcon="✍️"
    ButtonTitle="Write a new post"
    Placeholder="What's happening?"
    MaxLength="500"
    BottomPosition="24"
    RightPosition="24"
    OnPostCreated="HandleNewPost"
    CssClass="my-custom-fab" />

@code {
    private List<PostData> posts = new();
    
    private async Task HandleNewPost(Activity activity)
    {
        // Add the new post to the local list
        posts.Insert(0, new PostData 
        {
            Content = activity.Object?.FirstOrDefault()?.ToString() ?? "",
            CreatedAt = activity.Published ?? DateTimeOffset.UtcNow
        });
        
        StateHasChanged();
        
        // Optionally show a success message
        await ShowSuccessToast();
    }
    
    private async Task ShowSuccessToast()
    {
        // Your toast notification logic here
    }
    
    private class PostData
    {
        public string Content { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
    }
}
```

## Accessibility

The component includes:
- ARIA labels and titles
- Keyboard navigation support
- Focus management
- Screen reader compatible

## Browser Support

Compatible with all modern browsers:
- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers (iOS Safari, Chrome Mobile)

## Notes

- The file upload functionality requires implementation of actual file storage
- Update the `HandleFileSelected` method to integrate with your storage solution
- Media attachments, mentions, and tags are collected but need backend integration
- The component integrates with the existing `PostComposer` component for consistency
