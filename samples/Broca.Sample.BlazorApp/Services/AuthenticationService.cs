using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.JSInterop;

namespace Broca.Sample.BlazorApp.Services;

/// <summary>
/// Service for managing authentication state in the sample app
/// Integrates IdentityService with IActivityPubClient
/// </summary>
public class AuthenticationService : IDisposable
{
    private readonly IdentityService _identityService;
    private readonly IActivityPubClient _activityPubClient;
    private readonly IJSRuntime _jsRuntime;

    private const string LocalStorageIdentityKey = "broca.sample.identity";
    private const string LocalStorageSystemUserKey = "broca.sample.systemUser";

    public AuthenticationService(
        IdentityService identityService,
        IActivityPubClient activityPubClient,
        IJSRuntime jsRuntime)
    {
        _identityService = identityService;
        _activityPubClient = activityPubClient;
        _jsRuntime = jsRuntime;

        // Subscribe to identity changes
        _identityService.IdentityChanged += OnIdentityChanged;
    }

    /// <summary>
    /// Gets whether the user is currently authenticated
    /// </summary>
    public bool IsAuthenticated => _identityService.IsAuthenticated;

    /// <summary>
    /// Gets the current actor profile
    /// </summary>
    public Actor? CurrentActor { get; private set; }

    /// <summary>
    /// Gets the current actor ID
    /// </summary>
    public string? CurrentActorId => CurrentActor?.Id?.ToString();

    /// <summary>
    /// Gets the system user actor ID for anonymous browsing
    /// </summary>
    public string? SystemUserActorId { get; private set; }

    /// <summary>
    /// Event raised when authentication state changes
    /// </summary>
    public event EventHandler<bool>? AuthenticationStateChanged;

    /// <summary>
    /// Initializes the authentication service
    /// </summary>
    public async Task InitializeAsync()
    {
        // Try to load saved identity from localStorage
        await LoadSavedIdentityAsync();

        // Load system user configuration
        await LoadSystemUserAsync();
    }

    /// <summary>
    /// Logs in with an identity loaded from a JSON file
    /// </summary>
    public async Task<bool> LoginAsync(string identityJson)
    {
        try
        {
            var success = await _identityService.LoadIdentityFromFileAsync(identityJson);
            if (!success)
                return false;

            // Configure ActivityPub client with the identity
            await ConfigureClientWithIdentityAsync();

            // Try to fetch actor profile
            if (!string.IsNullOrEmpty(_identityService.CurrentIdentity?.ActorId))
            {
                try
                {
                    var actorUri = new Uri(_identityService.CurrentIdentity.ActorId);
                    CurrentActor = await _activityPubClient.GetActorAsync(actorUri);
                }
                catch
                {
                    // If we can't fetch the actor, that's okay for now
                }
            }

            // Save identity to localStorage
            await SaveIdentityAsync();

            AuthenticationStateChanged?.Invoke(this, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Logs out the current user
    /// </summary>
    public async Task LogoutAsync()
    {
        _identityService.ClearIdentity();
        CurrentActor = null;

        // Clear from localStorage
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", LocalStorageIdentityKey);

        // Reset client configuration
        await ConfigureClientWithIdentityAsync();

        AuthenticationStateChanged?.Invoke(this, false);
    }

    /// <summary>
    /// Sets the system user actor ID for anonymous browsing
    /// </summary>
    public async Task SetSystemUserAsync(string actorId)
    {
        SystemUserActorId = actorId;
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LocalStorageSystemUserKey, actorId);
    }

    /// <summary>
    /// Gets the inbox URL for the current user
    /// </summary>
    public Uri? GetInboxUrl()
    {
        if (CurrentActor is Person person && person.Inbox != null)
        {
            if (person.Inbox is Link link && link.Href != null)
                return link.Href;
        }

        return null;
    }

    /// <summary>
    /// Gets the outbox URL for the current user or system user
    /// </summary>
    public Uri? GetOutboxUrl()
    {
        if (IsAuthenticated && CurrentActor is Person person && person.Outbox != null)
        {
            if (person.Outbox is Link link && link.Href != null)
                return link.Href;
        }

        // For anonymous users, return system user outbox if configured
        if (!string.IsNullOrEmpty(SystemUserActorId))
        {
            // This would need to fetch the system user actor to get the outbox URL
            // For now, we'll construct a standard outbox URL
            return new Uri($"{SystemUserActorId}/outbox");
        }

        return null;
    }

    private Task ConfigureClientWithIdentityAsync()
    {
        if (_identityService.IsAuthenticated && _identityService.CurrentIdentity != null)
        {
            // TODO: Configure client with actor ID and private key
            // The IActivityPubClient.ActorId is read-only and should be configured
            // during client initialization, not at runtime. The client should be
            // recreated or properly initialized with the identity credentials.
            // This may require updating the client implementation to support
            // runtime reconfiguration.
            
            // The private key would need to be set through the cryptographic provider
            // This is a simplified version - actual implementation may vary
        }
        else
        {
            // TODO: Handle client cleanup when logged out
            // The client should be reset to anonymous mode
        }
        
        return Task.CompletedTask;
    }

    private async Task LoadSavedIdentityAsync()
    {
        try
        {
            var identityJson = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", LocalStorageIdentityKey);
            if (!string.IsNullOrEmpty(identityJson))
            {
                await LoginAsync(identityJson);
            }
        }
        catch
        {
            // If loading fails, just start fresh
        }
    }

    private async Task SaveIdentityAsync()
    {
        if (_identityService.CurrentIdentity != null)
        {
            var json = _identityService.ExportIdentityToJson();
            if (json != null)
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LocalStorageIdentityKey, json);
            }
        }
    }

    private async Task LoadSystemUserAsync()
    {
        try
        {
            var systemUser = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", LocalStorageSystemUserKey);
            if (!string.IsNullOrEmpty(systemUser))
            {
                SystemUserActorId = systemUser;
            }
        }
        catch
        {
            // If loading fails, system user will remain null
        }
    }

    private void OnIdentityChanged(object? sender, EventArgs e)
    {
        _ = ConfigureClientWithIdentityAsync();
    }

    public void Dispose()
    {
        _identityService.IdentityChanged -= OnIdentityChanged;
    }
}
