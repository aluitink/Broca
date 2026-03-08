using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Persistence.MySql.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.Persistence.MySql.Extensions;

public static class MySqlPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddMySqlPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContextFactory<BrocaDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        RegisterRepositories(services);
        return services;
    }

    public static async Task InitializeMySqlSchemaAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var factory = services.GetRequiredService<IDbContextFactory<BrocaDbContext>>();
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    public static IServiceCollection AddMySqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Broca")
            ?? configuration["Persistence:ConnectionString"]
            ?? throw new InvalidOperationException("No MySQL connection string found. Provide 'ConnectionStrings:Broca' or 'Persistence:ConnectionString'.");

        return services.AddMySqlPersistence(connectionString);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<MySqlActorRepository>();
        services.AddScoped<IActorRepository>(sp => sp.GetRequiredService<MySqlActorRepository>());
        services.AddScoped<IActorStatistics>(sp => sp.GetRequiredService<MySqlActorRepository>());

        services.AddScoped<MySqlActivityRepository>();
        services.AddScoped<IActivityRepository>(sp => sp.GetRequiredService<MySqlActivityRepository>());
        services.AddScoped<IActivityStatistics>(sp => sp.GetRequiredService<MySqlActivityRepository>());
        services.AddScoped<ISearchableActivityRepository>(sp => sp.GetRequiredService<MySqlActivityRepository>());

        services.AddScoped<IDeliveryQueueRepository, MySqlDeliveryQueueRepository>();
        services.AddScoped<IBlobStorageService, MySqlBlobStorageService>();
    }
}
