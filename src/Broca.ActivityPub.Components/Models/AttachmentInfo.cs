namespace Broca.ActivityPub.Components.Models;

public class AttachmentInfo
{
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long Size { get; set; }
    public string? AltText { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? ThumbnailUrl { get; set; }
}
