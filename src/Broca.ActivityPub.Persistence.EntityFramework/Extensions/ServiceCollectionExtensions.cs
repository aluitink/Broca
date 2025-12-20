using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Persistence.EntityFramework.Repositories;
using Broca.ActivityPub.Persistence.EntityFramework.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.Persistence.EntityFramework.Extensions;

/// <summary>
/// Extension methods for registering Entity Framework persistence services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Entity Framework-based ActivityPub persistence services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureDbContext">Action to configure the DbContext with a specific provider (SQL Server, PostgreSQL, SQLite, etc.)</param>
    /// <returns>Service collection for chaining</returns>
    /// <example>
    /// <code>
    /// // SQL Server
    /// services.AddActivityPubEntityFramework(options => 
    ///     options.UseSqlServer(connectionString));
    /// 
    /// // PostgreSQL
    /// services.AddActivityPubEntityFramework(options => 
    ///     options.UseNpgsql(connectionString));
    /// 
    /// // SQLite
    /// services.AddActivityPubEntityFramework(options => 
    ///     options.UseSqlite(connectionString));
    /// </code>
    /// </example>
    public static IServiceCollection AddActivityPubEntityFramework(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        // Register DbContext with the provided configuration
        services.AddDbContext<ActivityPubDbContext>(configureDbContext);

        // Register helper services
        services.AddScoped<ActivityStreamExtractor>();
        services.AddScoped<CountManager>();

        // Register repositories
        services.AddScoped<IActorRepository, EfActorRepository>();
        services.AddScoped<IActivityRepository, EfActivityRepository>();
        services.AddScoped<IDeliveryQueueRepository, EfDeliveryQueueRepository>();

        return services;
    }

    /// <summary>
    /// Applies Entity Framework migrations to the database
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <returns>Async task</returns>
    public static async Task MigrateActivityPubDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Ensures the ActivityPub database is created (for development/testing)
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <returns>Async task</returns>
    public static async Task EnsureActivityPubDatabaseCreatedAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
}
