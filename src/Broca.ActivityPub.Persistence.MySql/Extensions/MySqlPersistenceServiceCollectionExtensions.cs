using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Persistence.MySql.MySql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.Persistence.MySql.Extensions;

public static class MySqlPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddMySqlPersistence(
        this IServiceCollection services,
        string connectionString,
        string? baseUrl = null)
    {
        services.Configure<MySqlPersistenceOptions>(options =>
        {
            options.ConnectionString = connectionString;
            if (baseUrl is not null)
                options.BaseUrl = baseUrl;
        });

        services.AddPooledDbContextFactory<BrocaDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.AddScoped<MySqlActorRepository>();
        services.AddScoped<MySqlActivityRepository>();
        services.AddScoped<MySqlDeliveryQueueRepository>();
        services.AddScoped<MySqlBlobStorageService>();

        services.AddScoped<IActorRepository>(sp => sp.GetRequiredService<MySqlActorRepository>());
        services.AddScoped<IActorStatistics>(sp => sp.GetRequiredService<MySqlActorRepository>());
        services.AddScoped<IActivityRepository>(sp => sp.GetRequiredService<MySqlActivityRepository>());
        services.AddScoped<IActivityStatistics>(sp => sp.GetRequiredService<MySqlActivityRepository>());
        services.AddScoped<ISearchableActivityRepository>(sp => sp.GetRequiredService<MySqlActivityRepository>());
        services.AddScoped<IDeliveryQueueRepository>(sp => sp.GetRequiredService<MySqlDeliveryQueueRepository>());
        services.AddScoped<IBlobStorageService>(sp => sp.GetRequiredService<MySqlBlobStorageService>());

        return services;
    }

    public static IServiceCollection AddMySqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["Persistence:ConnectionString"]
            ?? throw new InvalidOperationException("Persistence:ConnectionString is required for MySQL driver.");
        var baseUrl = configuration["ActivityPub:BaseUrl"];
        return services.AddMySqlPersistence(connectionString, baseUrl);
    }

    public static async Task MigrateAsync(this IServiceProvider serviceProvider)
    {
        await using var db = await serviceProvider
            .GetRequiredService<IDbContextFactory<BrocaDbContext>>()
            .CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }
}
