using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Broca.Sample.BlazorApp;

/// <summary>
/// Service for managing user identity (public/private key pairs)
/// </summary>
public class IdentityService
{
    private readonly IJSRuntime _jsRuntime;
    private UserIdentity? _currentIdentity;

    public UserIdentity? CurrentIdentity => _currentIdentity;
    public bool IsAuthenticated => _currentIdentity != null;

    public event EventHandler? IdentityChanged;

    public IdentityService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Creates a new identity with generated RSA key pair
    /// </summary>
    public async Task<UserIdentity> CreateIdentityAsync(string username, string domain, string? actorId = null)
    {
        // Generate RSA key pair using JavaScript crypto API
        var keyPair = await _jsRuntime.InvokeAsync<RsaKeyPair>("generateRSAKeyPair");
        
        var identity = new UserIdentity
        {
            Username = username,
            Domain = domain,
            ActorId = actorId ?? $"https://{domain}/users/{username}",
            PublicKey = keyPair.PublicKey,
            PrivateKey = keyPair.PrivateKey
        };

        _currentIdentity = identity;
        IdentityChanged?.Invoke(this, EventArgs.Empty);
        
        return identity;
    }

    /// <summary>
    /// Exports the current identity to JSON string
    /// </summary>
    public string? ExportIdentityToJson()
    {
        if (_currentIdentity == null)
            return null;

        return JsonSerializer.Serialize(_currentIdentity, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Downloads the current identity as a JSON file
    /// </summary>
    public async Task DownloadIdentityAsync(string filename = "identity.json")
    {
        if (_currentIdentity == null)
            return;

        var json = ExportIdentityToJson();
        if (json == null)
            return;

        var bytes = Encoding.UTF8.GetBytes(json);
        var base64 = Convert.ToBase64String(bytes);

        await _jsRuntime.InvokeVoidAsync("downloadFile", filename, "application/json", base64);
    }

    /// <summary>
    /// Loads identity from a JSON file
    /// </summary>
    public Task<bool> LoadIdentityFromFileAsync(string jsonContent)
    {
        try
        {
            var identity = JsonSerializer.Deserialize<UserIdentity>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (identity == null || string.IsNullOrWhiteSpace(identity.PrivateKey))
            {
                return Task.FromResult(false);
            }

            _currentIdentity = identity;
            IdentityChanged?.Invoke(this, EventArgs.Empty);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Clears the current identity
    /// </summary>
    public void ClearIdentity()
    {
        _currentIdentity = null;
        IdentityChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Represents a user's identity with public/private key pair
/// </summary>
public class UserIdentity
{
    public string ActorId { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
}

/// <summary>
/// RSA key pair returned from JavaScript
/// </summary>
public class RsaKeyPair
{
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
}
