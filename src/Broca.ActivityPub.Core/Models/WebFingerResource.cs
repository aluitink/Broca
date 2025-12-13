using System.Text.Json.Serialization;

namespace Broca.ActivityPub.Core.Models;

/// <summary>
/// Represents a WebFinger resource response
/// </summary>
/// <remarks>
/// WebFinger is used to discover information about users and resources.
/// See RFC 7033: https://tools.ietf.org/html/rfc7033
/// </remarks>
public class WebFingerResource
{
    /// <summary>
    /// The subject URI (e.g., acct:user@domain.tld)
    /// </summary>
    [JsonPropertyName("subject")]
    public string Subject { get; set; }

    /// <summary>
    /// Alternative URIs for the subject
    /// </summary>
    [JsonPropertyName("aliases")]
    public List<string>? Aliases { get; set; }

    /// <summary>
    /// Links associated with the subject
    /// </summary>
    [JsonPropertyName("links")]
    public List<ResourceLink> Links { get; set; } = new();

    public WebFingerResource(string subject)
    {
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
    }
}
