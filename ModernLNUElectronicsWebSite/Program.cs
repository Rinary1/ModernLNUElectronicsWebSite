using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ModernLNUElectronicsWebSite;
using ModernLNUElectronicsWebSite.Content;
using ModernLNUElectronicsWebSite.Search;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<MirrorContentClient>();
builder.Services.AddScoped<SearchService>();

builder.Services.AddMudServices();

await builder.Build().RunAsync();
