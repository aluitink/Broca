using Broca.ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Broca.Sample.CustomProvider;

/// <summary>
/// Example: Custom identity provider for a multi-user blogging platform
/// This demonstrates how to integrate ActivityPub with an existing application
/// </summary>
public class BlogPlatformIdentityProvider : IIdentityProvider
{
    private readonly IBlogUserRepository _userRepository;
    private readonly ILogger<BlogPlatformIdentityProvider> _logger;
    private readonly string _blogUrl;

    public BlogPlatformIdentityProvider(
        IBlogUserRepository userRepository,
        IConfiguration configuration,
        ILogger<BlogPlatformIdentityProvider> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
        _blogUrl = configuration["Blog:BaseUrl"] ?? "https://blog.example.com";
    }

    /// <summary>
    /// Returns all blog authors who have opted into federation
    /// </summary>
    public async Task<IEnumerable<string>> GetUsernamesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Query your existing database for users who want to be on the fediverse
            var users = await _userRepository.GetActiveAuthorsAsync(cancellationToken);
            
            return users
                .Where(u => u.EnableActivityPub) // Only users who opted in
                .Select(u => u.Username)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving usernames from blog platform");
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Converts blog user data to ActivityPub identity details
    /// </summary>
    public async Task<IdentityDetails?> GetIdentityDetailsAsync(string username, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
            
            if (user == null || !user.EnableActivityPub)
            {
                return null;
            }

            // Build identity from existing user data
            var identity = new IdentityDetails
            {
                Username = user.Username,
                DisplayName = user.FullName ?? user.Username,
                Summary = BuildSummary(user),
                AvatarUrl = GetAvatarUrl(user),
                HeaderUrl = GetHeaderUrl(user),
                ActorType = ActorType.Person,
                IsBot = false,
                IsLocked = user.RequireFollowApproval,
                IsDiscoverable = user.PublicProfile,
                Fields = BuildProfileFields(user)
            };

            // If you're migrating from another ActivityPub server and have existing keys
            if (!string.IsNullOrEmpty(user.ActivityPubPublicKey) && 
                !string.IsNullOrEmpty(user.ActivityPubPrivateKey))
            {
                identity.Keys = new KeyPair
                {
                    PublicKey = user.ActivityPubPublicKey,
                    PrivateKey = user.ActivityPubPrivateKey
                };
            }
            // Otherwise, keys will be auto-generated

            return identity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving identity details for {Username}", username);
            return null;
        }
    }

    /// <summary>
    /// Checks if a user exists and is available for federation
    /// </summary>
    public async Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
            return user != null && user.EnableActivityPub;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user exists: {Username}", username);
            return false;
        }
    }

    // Helper methods

    private string BuildSummary(BlogUser user)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(user.Bio))
        {
            parts.Add(user.Bio);
        }

        if (!string.IsNullOrEmpty(user.Role))
        {
            parts.Add($"<p><em>{user.Role}</em></p>");
        }

        if (user.ArticleCount > 0)
        {
            parts.Add($"<p>📝 {user.ArticleCount} articles published</p>");
        }

        return parts.Any() ? string.Join("\n", parts) : $"Author at {_blogUrl}";
    }

    private string? GetAvatarUrl(BlogUser user)
    {
        if (string.IsNullOrEmpty(user.AvatarPath))
            return null;

        // Convert relative path to absolute URL
        return user.AvatarPath.StartsWith("http")
            ? user.AvatarPath
            : $"{_blogUrl.TrimEnd('/')}/{user.AvatarPath.TrimStart('/')}";
    }

    private string? GetHeaderUrl(BlogUser user)
    {
        if (string.IsNullOrEmpty(user.BannerPath))
            return null;

        return user.BannerPath.StartsWith("http")
            ? user.BannerPath
            : $"{_blogUrl.TrimEnd('/')}/{user.BannerPath.TrimStart('/')}";
    }

    private Dictionary<string, string>? BuildProfileFields(BlogUser user)
    {
        var fields = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(user.Website))
        {
            fields["Website"] = user.Website;
        }

        if (!string.IsNullOrEmpty(user.TwitterHandle))
        {
            fields["Twitter"] = $"https://twitter.com/{user.TwitterHandle.TrimStart('@')}";
        }

        if (!string.IsNullOrEmpty(user.GitHubUsername))
        {
            fields["GitHub"] = $"https://github.com/{user.GitHubUsername}";
        }

        if (!string.IsNullOrEmpty(user.Location))
        {
            fields["Location"] = user.Location;
        }

        // Link to their author page on your blog
        fields["Blog Profile"] = $"{_blogUrl}/authors/{user.Username}";

        return fields.Any() ? fields : null;
    }
}

// Example domain models (your existing entities)

public class BlogUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Bio { get; set; }
    public string? Role { get; set; }
    public string? AvatarPath { get; set; }
    public string? BannerPath { get; set; }
    public string? Website { get; set; }
    public string? TwitterHandle { get; set; }
    public string? GitHubUsername { get; set; }
    public string? Location { get; set; }
    public int ArticleCount { get; set; }
    
    // ActivityPub settings
    public bool EnableActivityPub { get; set; }
    public bool PublicProfile { get; set; }
    public bool RequireFollowApproval { get; set; }
    
    // Optional: Store existing keys if migrating
    public string? ActivityPubPublicKey { get; set; }
    public string? ActivityPubPrivateKey { get; set; }
}

public interface IBlogUserRepository
{
    Task<IEnumerable<BlogUser>> GetActiveAuthorsAsync(CancellationToken cancellationToken = default);
    Task<BlogUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
}

// Registration in Program.cs

/*
var builder = WebApplication.CreateBuilder(args);

// Your existing services
builder.Services.AddDbContext<BlogDbContext>();
builder.Services.AddScoped<IBlogUserRepository, BlogUserRepository>();

// Add ActivityPub
builder.Services.AddBrocaServer(builder.Configuration);
builder.Services.AddIdentityProvider<BlogPlatformIdentityProvider>();

var app = builder.Build();

// Initialize identities
var identityService = app.Services.GetRequiredService<IdentityProviderService>();
await identityService.InitializeIdentitiesAsync();

app.MapControllers();
app.Run();
*/
