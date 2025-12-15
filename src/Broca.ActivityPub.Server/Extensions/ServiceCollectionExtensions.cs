using Broca.ActivityPub.Client.Extensions;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.InMemory;
using Broca.ActivityPub.Server.Services;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Configuration;

namespace Broca.ActivityPub.Server.Extensions;

/// <summary>
/// Extension methods for configuring Broca ActivityPub Server
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Broca ActivityPub Server services to the DI container
    /// </summary>
    public static IServiceCollection AddActivityPubServer(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // Configure options
        ActivityPubServerOptions? serverOptions = null;
        if (configuration != null)
        {
            serverOptions = configuration.GetSection("ActivityPub").Get<ActivityPubServerOptions>();
            services.Configure<ActivityPubServerOptions>(configuration.GetSection("ActivityPub"));
        }
        else
        {
            serverOptions = new ActivityPubServerOptions();
            services.Configure<ActivityPubServerOptions>(options => { });
        }

        // Register ActivityPub client services (required for delivery and inbox processing)
        services.AddActivityPubClient();

        // Register memory cache (required for InboxController)
        services.AddMemoryCache();

        // Register repositories (in-memory by default)
        services.AddSingleton<IActorRepository, InMemoryActorRepository>();
        services.AddSingleton<IActivityRepository, InMemoryActivityRepository>();
        services.AddSingleton<IDeliveryQueueRepository, InMemoryDeliveryQueueRepository>();
        services.AddSingleton<IBlobStorageService, InMemoryBlobStorageService>();

        // Register services
        services.AddSingleton<CryptographyService>();
        services.AddScoped<IInboxHandler, InboxProcessor>();
        services.AddScoped<OutboxProcessor>();
        services.AddScoped<ActivityDeliveryService>();
        services.AddScoped<WebFingerService>();
        services.AddSingleton<ISystemIdentityService, SystemIdentityService>();
        services.AddSingleton<IActivityBuilderFactory, ActivityBuilderFactory>();
        services.AddScoped<AdminOperationsHandler>();
        services.AddScoped<AttachmentProcessingService>();
        services.AddScoped<ICollectionService, CollectionService>();

        // Register background worker for activity delivery
        services.AddHostedService<ActivityDeliveryWorker>();

        // Identity provider is not registered by default
        // Users should call AddSimpleIdentityProvider or AddIdentityProvider

        // Add controllers with JSON options for ActivityPub and route prefix convention
        var routePrefix = serverOptions?.RoutePrefix ?? string.Empty;
        services.AddControllers(options =>
        {
            if (!string.IsNullOrWhiteSpace(routePrefix))
            {
                options.Conventions.Add(new RoutePrefixConvention(routePrefix));
            }
        })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            });
        
        return services;
    }

    /// <summary>
    /// Adds simple identity provider (single user from configuration)
    /// </summary>
    /// <remarks>
    /// Ideal for personal blogs, single-user instances, or simple ActivityPub integration.
    /// Configure via appsettings.json under "IdentityProvider:SimpleIdentity" section.
    /// </remarks>
    public static IServiceCollection AddSimpleIdentityProvider(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // Configure identity provider options
        if (configuration != null)
        {
            services.Configure<IdentityProviderOptions>(configuration.GetSection("IdentityProvider"));
        }

        // Register the simple identity provider
        services.AddSingleton<IIdentityProvider, SimpleIdentityProvider>();
        services.AddSingleton<IdentityProviderService>();

        return services;
    }

    /// <summary>
    /// Adds a custom identity provider implementation
    /// </summary>
    /// <typeparam name="TProvider">Your IIdentityProvider implementation</typeparam>
    /// <remarks>
    /// Use this when you need to integrate with an existing database, CMS, or custom identity system.
    /// Your provider will be called to populate actors on startup and when requested.
    /// </remarks>
    public static IServiceCollection AddIdentityProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IIdentityProvider
    {
        services.AddSingleton<IIdentityProvider, TProvider>();
        services.AddSingleton<IdentityProviderService>();
        return services;
    }

    /// <summary>
    /// Adds a custom identity provider implementation with factory
    /// </summary>
    /// <remarks>
    /// Use this when you need control over how the identity provider is created,
    /// such as passing constructor parameters or using other services.
    /// </remarks>
    public static IServiceCollection AddIdentityProvider(
        this IServiceCollection services, 
        Func<IServiceProvider, IIdentityProvider> factory)
    {
        services.AddSingleton<IIdentityProvider>(factory);
        services.AddSingleton<IdentityProviderService>();
        return services;
    }

    /// <summary>
    /// Convention to add a route prefix to all controllers except WebFinger
    /// </summary>
    private class RoutePrefixConvention : IApplicationModelConvention
    {
        private readonly string _routePrefix;

        public RoutePrefixConvention(string routePrefix)
        {
            _routePrefix = routePrefix.Trim('/');
        }

        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                // Skip WebFinger controller - it must stay at .well-known/webfinger
                if (controller.ControllerName == "WebFinger")
                {
                    continue;
                }

                // Add route prefix to all route selectors
                foreach (var selector in controller.Selectors)
                {
                    if (selector.AttributeRouteModel != null)
                    {
                        var template = selector.AttributeRouteModel.Template;
                        selector.AttributeRouteModel.Template = string.IsNullOrWhiteSpace(template)
                            ? _routePrefix
                            : $"{_routePrefix}/{template}";
                    }
                }
            }
        }
    }
}

