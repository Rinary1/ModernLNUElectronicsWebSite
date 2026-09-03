using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ModernLNUElectronicsWebSite;
using ModernLNUElectronicsWebSite.Scraping;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<IHtmlSource, HttpHtmlSource>();
builder.Services.AddScoped<NewsScraper>();

builder.Services.AddMudServices();

await builder.Build().RunAsync();
