using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Persistence.FileSystem;
using Broca.ActivityPub.Persistence.InMemory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.Persistence.Extensions;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryPersistence(this IServiceCollection services)
    {
        services.AddSingleton<IActorRepository, InMemoryActorRepository>();
        services.AddSingleton<IActivityRepository, InMemoryActivityRepository>();
        services.AddSingleton<IBlobStorageService, InMemoryBlobStorageService>();
        services.AddSingleton<IDeliveryQueueRepository, InMemoryDeliveryQueueRepository>();

        return services;
    }

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
        services.AddSingleton<IDeliveryQueueRepository, FileSystemDeliveryQueueRepository>();

        return services;
    }

    public static IServiceCollection AddFileSystemPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FileSystemPersistenceOptions>(configuration.GetSection("Persistence"));

        services.AddSingleton<IActorRepository, FileSystemActorRepository>();
        services.AddSingleton<IActivityRepository, FileSystemActivityRepository>();
        services.AddSingleton<IBlobStorageService, FileSystemBlobStorageService>();
        services.AddSingleton<IDeliveryQueueRepository, FileSystemDeliveryQueueRepository>();

        return services;
    }
}
