using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Server.Controllers;

/// <summary>
/// Controller for retrieving activities by their full ID
/// Provides the canonical endpoint for dereferencing activity URLs
/// </summary>
[ApiController]
[Route("activities")]
public class ActivitiesController : ControllerBase
{
    private readonly IActivityRepository _activityRepository;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<ActivitiesController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ActivitiesController(
        IActivityRepository activityRepository,
        IOptions<ActivityPubServerOptions> options,
        ILogger<ActivitiesController> logger)
    {
        _activityRepository = activityRepository;
        _options = options.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Get an activity by its ID
    /// This endpoint allows ActivityPub clients to dereference activity URLs
    /// </summary>
    /// <param name="activityId">The activity ID (e.g., the GUID portion of the URL)</param>
    /// <returns>The activity object</returns>
    [HttpGet("{activityId}")]
    [Produces("application/activity+json", "application/ld+json")]
    public async Task<IActionResult> GetActivity(string activityId)
    {
        try
        {
            // Construct the full activity ID from the route parameter
            var baseUrl = (_options.BaseUrl ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');
            var routePrefix = _options.NormalizedRoutePrefix;
            var fullActivityId = $"{baseUrl}{routePrefix}/activities/{activityId}";

            _logger.LogDebug("Retrieving activity: {ActivityId}", fullActivityId);

            // Get the activity from the repository
            var activity = await _activityRepository.GetActivityByIdAsync(fullActivityId);
            if (activity == null)
            {
                _logger.LogWarning("Activity not found: {ActivityId}", fullActivityId);
                return NotFound(new { error = "Activity not found" });
            }

            // Ensure the activity has its ID set
            if (activity is IObject activityObj && string.IsNullOrEmpty(activityObj.Id))
            {
                activityObj.Id = fullActivityId;
            }

            return Ok(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving activity {ActivityId}", activityId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
