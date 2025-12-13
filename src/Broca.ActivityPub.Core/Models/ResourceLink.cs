using System.Text.Json.Serialization;

namespace Broca.ActivityPub.Core.Models;

/// <summary>
/// Represents a link in a WebFinger resource
/// </summary>
public class ResourceLink
{
    /// <summary>
    /// The relationship type of the link
    /// </summary>
    [JsonPropertyName("rel")]
    public string Rel { get; set; } = string.Empty;

    /// <summary>
    /// The MIME type of the linked resource
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// The URL of the linked resource
    /// </summary>
    [JsonPropertyName("href")]
    public string? Href { get; set; }

    /// <summary>
    /// Template URL with variables
    /// </summary>
    [JsonPropertyName("template")]
    public string? Template { get; set; }
}
