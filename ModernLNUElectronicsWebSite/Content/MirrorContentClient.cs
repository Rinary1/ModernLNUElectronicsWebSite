using System.Net.Http.Json;
using System.Text.Json;
using ModernLNUElectronicsWebSite.Scraping;

namespace ModernLNUElectronicsWebSite.Content;

public sealed class MirrorContentClient(HttpClient http)
{
    private readonly Dictionary<string, object?> _cache = new(StringComparer.Ordinal);

    public Task<MirrorPage?> TryGetNewsAsync(string slug, CancellationToken ct = default) =>
        TryGetAsync<MirrorPage>($"data/news/{slug}.json", ct);

    public Task<MirrorPage?> TryGetDepartmentAsync(string slug, CancellationToken ct = default) =>
        TryGetAsync<MirrorPage>($"data/departments/{slug}.json", ct);

    public Task<MirrorPage?> TryGetPageAsync(MirrorPageRef reference, CancellationToken ct = default) =>
        TryGetAsync<MirrorPage>($"data/pages/{reference.Group}-{reference.Slug}.json", ct);

    public Task<EmployeeProfile?> TryGetEmployeeAsync(string slug, CancellationToken ct = default) =>
        TryGetAsync<EmployeeProfile>($"data/employees/{slug}.json", ct);

    private async Task<T?> TryGetAsync<T>(string url, CancellationToken ct) where T : class
    {
        if (_cache.TryGetValue(url, out var cached))
            return (T?)cached;

        T? value = null;
        try
        {
            value = await http.GetFromJsonAsync<T>(url, ct);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or NotSupportedException)
        {
        }

        _cache[url] = value;
        return value;
    }
}
