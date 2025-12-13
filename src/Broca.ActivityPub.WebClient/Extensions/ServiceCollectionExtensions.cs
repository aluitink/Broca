using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.WebClient.Extensions;

/// <summary>
/// Extension methods for configuring Broca ActivityPub WebClient
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Broca ActivityPub WebClient services to the DI container
    /// </summary>
    public static IServiceCollection UseBrocaWebClient(this IServiceCollection services)
    {
        // WebClient services will be added here as they are implemented
        
        return services;
    }
}
