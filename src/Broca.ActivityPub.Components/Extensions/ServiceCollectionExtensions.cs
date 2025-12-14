using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.Components.Extensions;

/// <summary>
/// Extension methods for configuring Broca ActivityPub Components
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Broca ActivityPub Components services to the DI container
    /// </summary>
    public static IServiceCollection UseBrocaComponents(this IServiceCollection services)
    {
        // WebClient services will be added here as they are implemented
        
        return services;
    }
}
