using Broca.ActivityPub.Client.Extensions;
using Broca.ActivityPub.Server.Extensions;
using Broca.ActivityPub.Persistence.Extensions;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure ActivityPub base URL from environment or configuration
var baseUrl = builder.Configuration["ActivityPub:BaseUrl"] ?? "http://localhost:8080";
var primaryDomain = builder.Configuration["ActivityPub:PrimaryDomain"] ?? "localhost";
var routePrefix = builder.Configuration["ActivityPub:RoutePrefix"] ?? "ap";
var dataPath = builder.Configuration["Persistence:DataPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "data");

builder.Configuration["ActivityPub:BaseUrl"] = baseUrl;
builder.Configuration["ActivityPub:PrimaryDomain"] = primaryDomain;
builder.Configuration["ActivityPub:RoutePrefix"] = routePrefix;
builder.Configuration["Persistence:DataPath"] = dataPath;

// Add FileSystem persistence
builder.Services.AddFileSystemPersistence(dataPath);

// Add Broca ActivityPub services
builder.Services.AddActivityPubServer(builder.Configuration);
builder.Services.AddActivityPubClient();

// Add simple identity provider (configured via appsettings.json)
builder.Services.AddSimpleIdentityProvider(builder.Configuration);

var app = builder.Build();

// Initialize system actor on startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var systemIdentity = scope.ServiceProvider.GetRequiredService<ISystemIdentityService>();
    
    logger.LogInformation("Initializing system actor...");
    await systemIdentity.EnsureSystemActorAsync();
    logger.LogInformation("System actor initialized successfully");
    
    // Initialize identities from identity provider
    var identityService = scope.ServiceProvider.GetService<IdentityProviderService>();
    if (identityService != null)
    {
        logger.LogInformation("Initializing identity provider...");
        await identityService.InitializeIdentitiesAsync();
        logger.LogInformation("Identity provider initialized successfully");
    }
}

// Configure the HTTP request pipeline.

// Map ActivityPub controllers
app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
