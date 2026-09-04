
namespace ModernLNUElectronicsWebSite.Scraper.Scraping;

public sealed class CorsProxyHtmlSource(HttpClient http) : IHtmlSource
{
    private const string ProxyPrefix = "https://api.allorigins.win/raw?url=";

    public async Task<string> GetHtmlAsync(string url, CancellationToken ct = default)
    {
        var proxied = ProxyPrefix + Uri.EscapeDataString(url);
        using var response = await http.GetAsync(proxied, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
