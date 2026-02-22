using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.Linq;
using System.Text.Json;

namespace Broca.Web.Services;

/// <summary>
/// Manages authentication state for the web client using API key authentication
/// </summary>
public class AuthenticationStateService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IActivityPubClient _activityPubClient;
    private readonly IOptions<ActivityPubClientOptions> _clientOptions;
    private readonly ILogger<AuthenticationStateService> _logger;

    private const string LocalStorageApiKeyKey = "broca.apiKey";
    private const string LocalStorageActorIdKey = "broca.actorId";
    private const string LocalStorageActorDataKey = "broca.actorData";

    public AuthenticationStateService(
        IJSRuntime jsRuntime,
        IActivityPubClient activityPubClient,
        IOptions<ActivityPubClientOptions> clientOptions,
        ILogger<AuthenticationStateService> logger)
    {
        _jsRuntime = jsRuntime;
        _activityPubClient = activityPubClient;
        _clientOptions = clientOptions;
        _logger = logger;
    }

    /// <summary>
    /// Gets whether the user is currently authenticated
    /// </summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>
    /// Gets the current actor profile
    /// </summary>
    public Actor? CurrentActor { get; private set; }

    /// <summary>
    /// Gets the current actor ID
    /// </summary>
    public string? CurrentActorId => CurrentActor?.Id?.ToString();

    /// <summary>
    /// Event raised when authentication state changes
    /// </summary>
    public event EventHandler<bool>? AuthenticationStateChanged;

    /// <summary>
    /// Initializes the service by restoring authentication from local storage
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var apiKey = await GetFromLocalStorageAsync(LocalStorageApiKeyKey);
            var actorId = await GetFromLocalStorageAsync(LocalStorageActorIdKey);
            var actorDataJson = await GetFromLocalStorageAsync(LocalStorageActorDataKey);

            if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(actorId))
            {
                _logger.LogInformation("Restoring authentication for actor {ActorId}", actorId);
                
                // Restore actor data from local storage if available
                if (!string.IsNullOrWhiteSpace(actorDataJson))
                {
                    try
                    {
                        CurrentActor = JsonSerializer.Deserialize<Actor>(actorDataJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize stored actor data");
                    }
                }

                // Authenticate with the stored credentials
                await LoginAsync(actorId, apiKey, skipStorage: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize authentication state");
        }
    }

    /// <summary>
    /// Authenticates with an API key and retrieves the actor profile
    /// </summary>
    /// <param name="actorId">The actor ID (e.g., https://example.com/users/alice)</param>
    /// <param name="apiKey">The API key for authentication</param>
    /// <param name="skipStorage">Whether to skip saving to local storage</param>
    /// <returns>True if authentication succeeded</returns>
    public async Task<bool> LoginAsync(string actorId, string apiKey, bool skipStorage = false)
    {
        try
        {
            _logger.LogInformation("Attempting to login as {ActorId}", actorId);

            // Configure the ActivityPub client with the API key
            _clientOptions.Value.ActorId = actorId;
            _clientOptions.Value.ApiKey = apiKey;

            // Initialize the client to fetch actor profile and private key
            await _activityPubClient.InitializeAsync();

            // Fetch the full actor profile
            CurrentActor = await _activityPubClient.GetActorAsync(new Uri(actorId));
            
            if (CurrentActor == null)
            {
                _logger.LogWarning("Failed to fetch actor profile for {ActorId}", actorId);
                return false;
            }

            IsAuthenticated = true;

            // Save to local storage
            if (!skipStorage)
            {
                await SaveToLocalStorageAsync(LocalStorageApiKeyKey, apiKey);
                await SaveToLocalStorageAsync(LocalStorageActorIdKey, actorId);
                await SaveToLocalStorageAsync(LocalStorageActorDataKey, JsonSerializer.Serialize(CurrentActor));
            }

            _logger.LogInformation("Successfully authenticated as {ActorId}", actorId);
            
            // Notify listeners
            AuthenticationStateChanged?.Invoke(this, true);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to login with API key");
            await LogoutAsync();
            return false;
        }
    }

    /// <summary>
    /// Logs out the current user and clears authentication state
    /// </summary>
    public async Task LogoutAsync()
    {
        _logger.LogInformation("Logging out");

        IsAuthenticated = false;
        CurrentActor = null;
        
        _clientOptions.Value.ActorId = null;
        _clientOptions.Value.ApiKey = null;
        _clientOptions.Value.PrivateKeyPem = null;
        _clientOptions.Value.PublicKeyId = null;

        // Clear local storage
        await RemoveFromLocalStorageAsync(LocalStorageApiKeyKey);
        await RemoveFromLocalStorageAsync(LocalStorageActorIdKey);
        await RemoveFromLocalStorageAsync(LocalStorageActorDataKey);

        // Notify listeners
        AuthenticationStateChanged?.Invoke(this, false);
    }

    /// <summary>
    /// Gets the inbox URL for the current actor
    /// </summary>
    public Uri? GetInboxUrl()
    {
        return CurrentActor?.Inbox?.Href;
    }

    /// <summary>
    /// Gets the outbox URL for the current actor
    /// </summary>
    public Uri? GetOutboxUrl()
    {
        return CurrentActor?.Outbox?.Href;
    }

    /// <summary>
    /// Gets the followers collection URL for the current actor
    /// </summary>
    public Uri? GetFollowersUrl()
    {
        return CurrentActor?.Followers?.Href;
    }

    /// <summary>
    /// Gets the following collection URL for the current actor
    /// </summary>
    public Uri? GetFollowingUrl()
    {
        return CurrentActor?.Following?.Href;
    }
    /// <summary>
    /// Refreshes the current actor data with updated information
    /// </summary>
    /// <param name="updatedActor">The updated actor object</param>
    public async Task RefreshActorAsync(Actor updatedActor)
    {
        if (!IsAuthenticated || CurrentActor == null)
        {
            _logger.LogWarning("Cannot refresh actor - not authenticated");
            return;
        }

        _logger.LogInformation("Refreshing actor data for {ActorId}", updatedActor.Id);
        
        // Update the current actor
        CurrentActor = updatedActor;

        // Save to local storage
        await SaveToLocalStorageAsync(LocalStorageActorDataKey, JsonSerializer.Serialize(CurrentActor));

        // Notify listeners
        AuthenticationStateChanged?.Invoke(this, true);
    }
    private async Task<string?> GetFromLocalStorageAsync(string key)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get item from local storage: {Key}", key);
            return null;
        }
    }

    private async Task SaveToLocalStorageAsync(string key, string value)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save item to local storage: {Key}", key);
        }
    }

    private async Task RemoveFromLocalStorageAsync(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove item from local storage: {Key}", key);
        }
    }
}
