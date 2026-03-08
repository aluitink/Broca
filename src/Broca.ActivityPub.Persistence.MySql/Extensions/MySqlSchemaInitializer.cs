using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Persistence.MySql.Extensions;

internal sealed class MySqlSchemaInitializer : IHostedService
{
    private readonly IDbContextFactory<BrocaDbContext> _contextFactory;
    private readonly ILogger<MySqlSchemaInitializer> _logger;

    public MySqlSchemaInitializer(
        IDbContextFactory<BrocaDbContext> contextFactory,
        ILogger<MySqlSchemaInitializer> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ensuring MySQL schema is up to date...");
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.EnsureCreatedAsync(cancellationToken);
        _logger.LogInformation("MySQL schema ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
