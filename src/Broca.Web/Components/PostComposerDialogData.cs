using KristofferStrube.ActivityStreams;
using Microsoft.AspNetCore.Components;

namespace Broca.Web.Components;

/// <summary>
/// Data passed to the PostComposerDialog
/// </summary>
public class PostComposerDialogData
{
    /// <summary>
    /// Title of the dialog
    /// </summary>
    public string Title { get; set; } = "Create Post";

    /// <summary>
    /// Placeholder text for the composer
    /// </summary>
    public string Placeholder { get; set; } = "What's on your mind?";

    /// <summary>
    /// Maximum content length
    /// </summary>
    public int MaxLength { get; set; } = 500;

    /// <summary>
    /// Number of rows for the textarea
    /// </summary>
    public int Rows { get; set; } = 8;

    /// <summary>
    /// Accepted file types for attachments
    /// </summary>
    public string AcceptedFileTypes { get; set; } = "image/*,video/*";

    /// <summary>
    /// ID of the activity this is in reply to (if any)
    /// </summary>
    public string? InReplyToId { get; set; }

    /// <summary>
    /// Callback for when a post is successfully created
    /// </summary>
    public EventCallback<Activity> OnPostCreated { get; set; }
}
