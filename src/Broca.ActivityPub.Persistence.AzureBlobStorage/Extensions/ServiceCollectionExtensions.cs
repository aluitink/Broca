using Broca.ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.Persistence.AzureBlobStorage.Extensions;

/// <summary>
/// Extension methods for registering Azure Blob Storage services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Azure Blob Storage as the blob storage provider
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration section for Azure Blob Storage</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAzureBlobStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AzureBlobStorageOptions>(configuration);
        services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

        return services;
    }

    /// <summary>
    /// Adds Azure Blob Storage as the blob storage provider
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Action to configure Azure Blob Storage options</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAzureBlobStorage(
        this IServiceCollection services,
        Action<AzureBlobStorageOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

        return services;
    }

    /// <summary>
    /// Adds Azure Blob Storage as the blob storage provider with a connection string
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">Azure Storage connection string</param>
    /// <param name="containerName">Container name (defaults to "activitypub-blobs")</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAzureBlobStorage(
        this IServiceCollection services,
        string connectionString,
        string containerName = "activitypub-blobs")
    {
        services.Configure<AzureBlobStorageOptions>(options =>
        {
            options.ConnectionString = connectionString;
            options.ContainerName = containerName;
        });
        services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

        return services;
    }
}
