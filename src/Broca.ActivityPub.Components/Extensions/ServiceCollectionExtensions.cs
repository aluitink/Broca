using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Client.Extensions;
using Broca.ActivityPub.Components.Services;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Broca.ActivityPub.Components.Extensions;

/// <summary>
/// Extension methods for configuring Broca ActivityPub Components
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Broca ActivityPub Components services to the DI container.
    /// This includes the ActivityPub client, object renderer registry, and default renderers.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Optional configuration action for component options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddActivityPubComponents(
        this IServiceCollection services,
        Action<ActivityPubComponentOptions>? configure = null)
    {
        var options = new ActivityPubComponentOptions();
        configure?.Invoke(options);

        // Register the options
        services.TryAddSingleton(options);

        // Register the ActivityPub client if not already registered
        if (!services.Any(x => x.ServiceType == typeof(IActivityPubClient)))
        {
            services.AddActivityPubClient();
        }

        // Register the object renderer registry
        services.TryAddSingleton<IObjectRendererRegistry, ObjectRendererRegistry>();

        // Register actor resolution service
        services.TryAddScoped<ActorResolutionService>();

        // Register default renderers
        RegisterDefaultRenderers(services);

        return services;
    }

    /// <summary>
    /// Adds Broca ActivityPub Components services to the DI container (legacy method)
    /// </summary>
    public static IServiceCollection UseBrocaComponents(this IServiceCollection services)
    {
        return services.AddActivityPubComponents();
    }

    /// <summary>
    /// Registers a custom object renderer for a specific object type.
    /// </summary>
    /// <typeparam name="TObject">The ActivityStreams object type.</typeparam>
    /// <typeparam name="TRenderer">The renderer component type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddObjectRenderer<TObject, TRenderer>(
        this IServiceCollection services)
        where TRenderer : IObjectRenderer
    {
        services.Configure<ActivityPubComponentOptions>(options =>
        {
            options.RegisterRenderer(typeof(TObject), typeof(TRenderer));
        });

        return services;
    }

    private static void RegisterDefaultRenderers(IServiceCollection services)
    {
        // Register default renderers for common ActivityStreams types
        // These are registered as a post-configuration step that populates the registry
        services.AddOptions<ActivityPubComponentOptions>()
            .PostConfigure<IServiceProvider>((options, sp) =>
            {
                var registry = sp.GetService<IObjectRendererRegistry>();
                if (registry != null)
                {
                    registry.RegisterRenderer(typeof(Note), new DefaultNoteRendererProxy());
                    registry.RegisterRenderer(typeof(Article), new DefaultArticleRendererProxy());
                    registry.RegisterRenderer(typeof(Image), new DefaultImageRendererProxy());
                    registry.RegisterRenderer(typeof(Video), new DefaultVideoRendererProxy());
                    registry.RegisterRenderer(typeof(Document), new DefaultDocumentRendererProxy());
                    registry.RegisterRenderer(typeof(Person), new DefaultActorRendererProxy());
                    registry.RegisterRenderer(typeof(Actor), new DefaultActorRendererProxy());
                    registry.RegisterRenderer(typeof(Activity), new DefaultActivityRendererProxy());
                }
            });
    }
}

/// <summary>
/// Configuration options for ActivityPub components.
/// </summary>
public class ActivityPubComponentOptions
{
    private readonly Dictionary<Type, Type> _rendererMap = new();

    /// <summary>
    /// Gets or sets the default page size for collection loaders.
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum number of items to keep in memory for virtualized collections.
    /// </summary>
    public int VirtualizationOverscan { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to automatically fetch actor details when rendering activities.
    /// </summary>
    public bool AutoFetchActors { get; set; } = true;

    /// <summary>
    /// Registers a renderer type for a specific object type.
    /// </summary>
    internal void RegisterRenderer(Type objectType, Type rendererType)
    {
        _rendererMap[objectType] = rendererType;
    }

    /// <summary>
    /// Gets the renderer type for a specific object type.
    /// </summary>
    internal Type? GetRendererType(Type objectType)
    {
        return _rendererMap.TryGetValue(objectType, out var rendererType) 
            ? rendererType 
            : null;
    }

    /// <summary>
    /// Gets all registered renderer mappings.
    /// </summary>
    internal IReadOnlyDictionary<Type, Type> GetRendererMap() => _rendererMap;
}
