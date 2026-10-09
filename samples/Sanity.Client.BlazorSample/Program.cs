using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Sanity.Client;
using Sanity.Client.BlazorSample;
using Sanity.Client.Presentation;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Registers ISanityClient, with options bound from the "Sanity" section of
// wwwroot/appsettings.json (ProjectId, Dataset, ApiVersion, UseCdn, ...).
builder.Services.AddSanityClient(builder.Configuration);

// Registers ISanityPresentationService (stega encoding + visual editing
// overlays), with options bound from the "SanityPresentation" section.
builder.Services.AddSanityVisualEditing(builder.Configuration);

await builder.Build().RunAsync();
