namespace Broca.ActivityPub.Core.Models;

/// <summary>
/// Configuration options for Broca ActivityPub
/// </summary>
public class ActivityPubOptions
{
    public string Domain { get; set; } = string.Empty;
    public string UserAgent { get; set; } = "Broca.ActivityPub/1.0";
    public bool EnableHttpSignatures { get; set; } = true;
}
