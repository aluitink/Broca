using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Broca.Web;
using Broca.Web.Services;
using Broca.Web.Renderers;
using Broca.ActivityPub.Components.Extensions;
using Broca.ActivityPub.Components.Services;
using Broca.ActivityPub.Client.WebCrypto.Extensions;
using Broca.ActivityPub.Core.Interfaces;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for API calls
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

// Add Fluent UI services
builder.Services.AddFluentUIComponents();

// Add Broca ActivityPub Client with WebCrypto for WASM
builder.Services.AddActivityPubClientWithWebCrypto(options =>
{
    // Options will be configured at runtime via AuthenticationStateService
});

// Override IActivityPubClient with the resilient wrapper that falls back to the
// server-side proxy when direct browser requests are blocked by CORS or authorized-fetch.
builder.Services.AddScoped<Broca.Web.Services.ProxyService>();
builder.Services.AddScoped<IActivityPubClient>(sp =>
    new Broca.Web.Services.ResilientActivityPubClient(
        sp.GetRequiredService<Broca.ActivityPub.Client.Services.ActivityPubClient>(),
        sp.GetRequiredService<Broca.Web.Services.ProxyService>(),
        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Broca.Web.Services.ResilientActivityPubClient>>()));

// Add Broca ActivityPub Components
builder.Services.AddActivityPubComponents(options =>
{
    options.DefaultPageSize = 20;
    options.AutoFetchActors = true;
    options.VirtualizationOverscan = 5;
});

// Add authentication state service
builder.Services.AddScoped<AuthenticationStateService>();

// Add collection service
builder.Services.AddScoped<ICollectionService, ClientCollectionService>();

var host = builder.Build();

// Initialize authentication state service
var authStateService = host.Services.GetRequiredService<AuthenticationStateService>();
await authStateService.InitializeAsync();

// Register Fluent UI renderers
var rendererRegistry = host.Services.GetRequiredService<IObjectRendererRegistry>();
rendererRegistry.RegisterFluentRenderers();

await host.RunAsync();

