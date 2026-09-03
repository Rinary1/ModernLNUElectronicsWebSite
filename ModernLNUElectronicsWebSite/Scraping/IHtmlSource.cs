namespace ModernLNUElectronicsWebSite.Scraping;

public interface IHtmlSource
{
    Task<string> GetHtmlAsync(string url, CancellationToken ct = default);
}
