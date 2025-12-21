using Broca.ActivityPub.Components.Services;
using KristofferStrube.ActivityStreams;
using Microsoft.AspNetCore.Components;

namespace Broca.Web.Renderers;

/// <summary>
/// Extension methods for registering Fluent UI renderers.
/// </summary>
public static class FluentRendererExtensions
{
    /// <summary>
    /// Registers all Fluent UI renderers with the object renderer registry.
    /// </summary>
    /// <param name="registry">The renderer registry.</param>
    public static void RegisterFluentRenderers(this IObjectRendererRegistry registry)
    {
        // For now, we'll use the default renderers from the Components library
        // TODO: Implement custom Fluent UI renderers that work with the component architecture
        
        // The ObjectDisplay component will use any custom renderers registered here
        // or fall back to the default rendering logic
    }
}
