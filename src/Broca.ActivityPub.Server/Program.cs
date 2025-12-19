using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure ActivityPub base URL
builder.Configuration["ActivityPub:BaseUrl"] = builder.Environment.IsDevelopment()
    ? "https://localhost:7001"
    : builder.Configuration["ActivityPub:BaseUrl"];

builder.Configuration["ActivityPub:PrimaryDomain"] = builder.Environment.IsDevelopment()
    ? "localhost"
    : builder.Configuration["ActivityPub:PrimaryDomain"];

// Add Broca ActivityPub Server services
builder.Services.AddActivityPubServer(builder.Configuration);

var app = builder.Build();

// Initialize system actor on startup
using (var scope = app.Services.CreateScope())
{
    var systemIdentity = scope.ServiceProvider.GetRequiredService<ISystemIdentityService>();
    await systemIdentity.EnsureSystemActorAsync();
}

// Configure the HTTP request pipeline
app.UseHttpsRedirection();

app.MapControllers();

app.Run();

// Make Program accessible to tests
namespace Broca.API
{
    public partial class Program { }
}