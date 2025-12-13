using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Persistence.FileSystem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Persistence.Extensions;

/// <summary>
/// Extension methods for configuring persistence services
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Adds FileSystem-based persistence services to the DI container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="dataPath">The root path where data will be stored</param>
    public static IServiceCollection AddFileSystemPersistence(
        this IServiceCollection services, 
        string dataPath)
    {
        services.Configure<FileSystemPersistenceOptions>(options =>
        {
            options.DataPath = dataPath;
        });

        services.AddSingleton<IActorRepository, FileSystemActorRepository>();
        services.AddSingleton<IActivityRepository, FileSystemActivityRepository>();
        services.AddSingleton<IBlobStorageService, FileSystemBlobStorageService>();

        return services;
    }

    /// <summary>
    /// Adds FileSystem-based persistence services to the DI container using configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing Persistence:DataPath</param>
    public static IServiceCollection AddFileSystemPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FileSystemPersistenceOptions>(configuration.GetSection("Persistence"));

        services.AddSingleton<IActorRepository, FileSystemActorRepository>();
        services.AddSingleton<IActivityRepository, FileSystemActivityRepository>();
        services.AddSingleton<IBlobStorageService, FileSystemBlobStorageService>();

        return services;
    }
}
