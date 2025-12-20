using Broca.ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.Persistence.FileSystem.Extensions;

/// <summary>
/// Extension methods for registering File System blob storage services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds File System blob storage as the blob storage provider
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration section for File System blob storage</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddFileSystemBlobStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FileSystemBlobStorageOptions>(configuration);
        services.AddSingleton<IBlobStorageService, FileSystemBlobStorageService>();

        return services;
    }

    /// <summary>
    /// Adds File System blob storage as the blob storage provider
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Action to configure File System blob storage options</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddFileSystemBlobStorage(
        this IServiceCollection services,
        Action<FileSystemBlobStorageOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IBlobStorageService, FileSystemBlobStorageService>();

        return services;
    }

    /// <summary>
    /// Adds File System blob storage with simple configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="dataPath">Root directory path for storing blobs</param>
    /// <param name="baseUrl">Base URL for accessing blobs</param>
    /// <param name="routePrefix">Route prefix for blob URLs (default: "/blobs")</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddFileSystemBlobStorage(
        this IServiceCollection services,
        string dataPath,
        string baseUrl,
        string routePrefix = "/blobs")
    {
        services.Configure<FileSystemBlobStorageOptions>(options =>
        {
            options.DataPath = dataPath;
            options.BaseUrl = baseUrl;
            options.RoutePrefix = routePrefix;
        });
        services.AddSingleton<IBlobStorageService, FileSystemBlobStorageService>();

        return services;
    }
}
