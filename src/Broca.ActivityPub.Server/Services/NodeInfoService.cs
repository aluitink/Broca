using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Service for NodeInfo protocol support
/// Provides instance metadata for fediverse discovery
/// </summary>
public class NodeInfoService
{
    private readonly IActorRepository _actorRepository;
    private readonly IActivityRepository _activityRepository;
    private readonly ILogger<NodeInfoService> _logger;
    private readonly ActivityPubServerOptions _options;

    public NodeInfoService(
        IActorRepository actorRepository,
        IActivityRepository activityRepository,
        ILogger<NodeInfoService> logger,
        IOptions<ActivityPubServerOptions> options)
    {
        _actorRepository = actorRepository;
        _activityRepository = activityRepository;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Gets the NodeInfo 2.0 document
    /// </summary>
    public async Task<object> GetNodeInfo20Async(CancellationToken cancellationToken = default)
    {
        var stats = await GetInstanceStatsAsync(cancellationToken);
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var routePrefix = _options.NormalizedRoutePrefix;

        return new
        {
            version = "2.0",
            software = new
            {
                name = "broca",
                version = GetVersion()
            },
            protocols = new[] { "activitypub" },
            services = new
            {
                outbound = Array.Empty<string>(),
                inbound = Array.Empty<string>()
            },
            usage = new
            {
                users = new
                {
                    total = stats.TotalUsers,
                    activeMonth = stats.ActiveUsersMonth,
                    activeHalfyear = stats.ActiveUsersHalfYear
                },
                localPosts = stats.LocalPosts
            },
            openRegistrations = false,
            metadata = new
            {
                nodeName = _options.ServerName,
                nodeDescription = _options.ServerDescription ?? "A Broca ActivityPub instance"
            }
        };
    }

    /// <summary>
    /// Gets the NodeInfo 2.1 document (includes repository info)
    /// </summary>
    public async Task<object> GetNodeInfo21Async(CancellationToken cancellationToken = default)
    {
        var stats = await GetInstanceStatsAsync(cancellationToken);
        var baseUrl = _options.BaseUrl.TrimEnd('/');

        return new
        {
            version = "2.1",
            software = new
            {
                name = "broca",
                version = GetVersion(),
                repository = "https://github.com/yourusername/broca"
            },
            protocols = new[] { "activitypub" },
            services = new
            {
                outbound = Array.Empty<string>(),
                inbound = Array.Empty<string>()
            },
            usage = new
            {
                users = new
                {
                    total = stats.TotalUsers,
                    activeMonth = stats.ActiveUsersMonth,
                    activeHalfyear = stats.ActiveUsersHalfYear
                },
                localPosts = stats.LocalPosts
            },
            openRegistrations = false,
            metadata = new
            {
                nodeName = _options.ServerName,
                nodeDescription = _options.ServerDescription ?? "A Broca ActivityPub instance"
            }
        };
    }

    /// <summary>
    /// Gets the NodeInfo discovery document
    /// </summary>
    public object GetNodeInfoDiscovery()
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');

        return new
        {
            links = new[]
            {
                new
                {
                    rel = "http://nodeinfo.diaspora.software/ns/schema/2.0",
                    href = $"{baseUrl}/nodeinfo/2.0"
                },
                new
                {
                    rel = "http://nodeinfo.diaspora.software/ns/schema/2.1",
                    href = $"{baseUrl}/nodeinfo/2.1"
                }
            }
        };
    }

    /// <summary>
    /// Gets the x-nodeinfo2 document (combined format used by some implementations)
    /// </summary>
    public async Task<object> GetXNodeInfo2Async(CancellationToken cancellationToken = default)
    {
        var stats = await GetInstanceStatsAsync(cancellationToken);
        
        return new
        {
            version = "1.0",
            server = new
            {
                baseUrl = _options.BaseUrl.TrimEnd('/'),
                name = _options.ServerName,
                software = "broca",
                version = GetVersion()
            },
            protocols = new[] { "activitypub" },
            services = new
            {
                outbound = Array.Empty<string>(),
                inbound = Array.Empty<string>()
            },
            openRegistrations = false,
            usage = new
            {
                users = new
                {
                    total = stats.TotalUsers,
                    activeMonth = stats.ActiveUsersMonth,
                    activeHalfyear = stats.ActiveUsersHalfYear
                },
                localPosts = stats.LocalPosts,
                localComments = 0
            },
            metadata = new
            {
                nodeName = _options.ServerName,
                nodeDescription = _options.ServerDescription ?? "A Broca ActivityPub instance"
            }
        };
    }

    private Task<InstanceStats> GetInstanceStatsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // TODO: Add methods to IActorRepository and IActivityRepository to get counts
            // For now, return minimal stats
            return Task.FromResult(new InstanceStats
            {
                TotalUsers = 1,
                ActiveUsersMonth = 1,
                ActiveUsersHalfYear = 1,
                LocalPosts = 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting instance stats");
            return Task.FromResult(new InstanceStats());
        }
    }

    private string GetVersion()
    {
        var assembly = typeof(NodeInfoService).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString(3) ?? "1.0.0";
    }

    private class InstanceStats
    {
        public int TotalUsers { get; set; }
        public int ActiveUsersMonth { get; set; }
        public int ActiveUsersHalfYear { get; set; }
        public int LocalPosts { get; set; }
    }
}
