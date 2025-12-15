using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Broca.Web;
using Broca.ActivityPub.Components.Extensions;
using Broca.ActivityPub.Client.Extensions;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for API calls
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

// Add Fluent UI services
builder.Services.AddFluentUIComponents();

// Add Broca ActivityPub services
builder.Services.AddActivityPubComponents(options =>
{
    options.DefaultPageSize = 20;
    options.AutoFetchActors = true;
    options.VirtualizationOverscan = 5;
});

await builder.Build().RunAsync();
