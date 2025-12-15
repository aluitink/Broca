using Broca.ActivityPub.Client.Extensions;
using Broca.ActivityPub.Components.Extensions;
using Broca.ActivityPub.Core.Models;
using Broca.Sample.BlazorApp;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for standard requests
builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) 
});

// Configure HttpClient for ActivityPub API calls
builder.Services.AddHttpClient("ActivityPubApi", client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/activity+json");
});

// Configure ActivityPub Client from configuration
// This allows both static configuration from appsettings and dynamic configuration at runtime
var activityPubClientSection = builder.Configuration.GetSection("ActivityPubClient");
builder.Services.Configure<ActivityPubClientOptions>(activityPubClientSection);

// Add Broca ActivityPub Components and Client services
builder.Services.AddActivityPubComponents(options =>
{
    options.DefaultPageSize = 20;
    options.AutoFetchActors = true;
});

// Add identity service for managing user keys
builder.Services.AddSingleton<IdentityService>();

await builder.Build().RunAsync();
