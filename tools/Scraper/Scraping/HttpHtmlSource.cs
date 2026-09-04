
namespace ModernLNUElectronicsWebSite.Scraper.Scraping;

public sealed class HttpHtmlSource(HttpClient http) : IHtmlSource
{
    public async Task<string> GetHtmlAsync(string url, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
