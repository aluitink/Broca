using System.Security.Cryptography;
using System.Text;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Server.Controllers;

/// <summary>
/// Serves remote actor media (icon and banner) with server-side caching.
/// </summary>
/// <remarks>
/// Avoids browser CORS issues by proxying and caching remote actor images.
/// Images are stored deterministically under the system actor's blob namespace
/// using a SHA-256 hash of the actor ID, so the UI can construct the URL
/// without any prior knowledge of whether the image is already cached.
/// 
/// Cache key pattern: actor-media/{sha256(actorId)}/icon  or  .../banner
/// Served at: GET /media/remote-actor?actorId={url}&type=icon|banner
/// </remarks>
[ApiController]
[Route("media/remote-actor")]
public class RemoteActorMediaController : ControllerBase
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IActivityPubClient _activityPubClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _sysUsername;
    private readonly ILogger<RemoteActorMediaController> _logger;

    public RemoteActorMediaController(
        IBlobStorageService blobStorage,
        IActivityPubClient activityPubClient,
        IHttpClientFactory httpClientFactory,
        IOptions<ActivityPubServerOptions> options,
        ILogger<RemoteActorMediaController> logger)
    {
        _blobStorage = blobStorage;
        _activityPubClient = activityPubClient;
        _httpClientFactory = httpClientFactory;
        _sysUsername = options.Value.SystemActorUsername;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string actorId,
        [FromQuery] string type = "icon",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return BadRequest(new { error = "actorId is required" });

        if (!Uri.TryCreate(actorId, UriKind.Absolute, out var actorUri))
            return BadRequest(new { error = "Invalid actorId format" });

        if (type != "icon" && type != "banner")
            return BadRequest(new { error = "type must be 'icon' or 'banner'" });

        var blobKey = BuildBlobKey(actorId, type);

        if (await _blobStorage.BlobExistsAsync(_sysUsername, blobKey, cancellationToken))
        {
            var cached = await _blobStorage.GetBlobAsync(_sysUsername, blobKey, cancellationToken);
            if (cached.HasValue)
            {
                Response.Headers.CacheControl = "public, max-age=86400";
                return File(cached.Value.Content, cached.Value.ContentType);
            }
        }

        Actor actor;
        try
        {
            actor = await _activityPubClient.GetActorAsync(actorUri, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch actor {ActorId} for media caching", actorId);
            return NotFound(new { error = "Actor not found or unavailable" });
        }

        var imageUrl = type == "icon" ? GetIconUrl(actor) : GetBannerUrl(actor);
        if (string.IsNullOrEmpty(imageUrl))
            return NotFound(new { error = $"Actor has no {type} image" });

        byte[] imageBytes;
        string contentType;
        try
        {
            using var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            var response = await http.GetAsync(imageUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Remote image {Url} responded {Status}", imageUrl, response.StatusCode);
                return NotFound(new { error = "Remote image unavailable" });
            }

            imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download {Type} from {Url}", type, imageUrl);
            return NotFound(new { error = "Failed to download image" });
        }

        try
        {
            await _blobStorage.StoreBlobAsync(
                _sysUsername,
                blobKey,
                new MemoryStream(imageBytes),
                contentType,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache {Type} for actor {ActorId}", type, actorId);
        }

        Response.Headers.CacheControl = "public, max-age=86400";
        return File(imageBytes, contentType);
    }

    private static string BuildBlobKey(string actorId, string type)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actorId))).ToLowerInvariant();
        return $"actor-media/{hash}/{type}";
    }

    private static string? GetIconUrl(Actor actor)
    {
        if (actor.Icon?.Any() != true) return null;
        var icon = actor.Icon.First();
        if (icon is Image img && img.Url?.Any() == true)
            return img.Url.First().Href?.ToString();
        if (icon is ILink link)
            return link.Href?.ToString();
        return null;
    }

    private static string? GetBannerUrl(Actor actor)
    {
        if (actor.Image?.Any() != true) return null;
        var image = actor.Image.First();
        if (image is Image img && img.Url?.Any() == true)
            return img.Url.First().Href?.ToString();
        if (image is ILink link)
            return link.Href?.ToString();
        return null;
    }
}
