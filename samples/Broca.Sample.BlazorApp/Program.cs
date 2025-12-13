using Broca.ActivityPub.Client.Extensions;
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

// Add Broca ActivityPub Client services
builder.Services.AddActivityPubClient();

// Add identity service for managing user keys
builder.Services.AddSingleton<IdentityService>();

await builder.Build().RunAsync();
