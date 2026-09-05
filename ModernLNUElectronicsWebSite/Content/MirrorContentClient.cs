using System.Net.Http.Json;
using System.Text.Json;
using ModernLNUElectronicsWebSite.Data;

namespace ModernLNUElectronicsWebSite.Content;

public sealed class MirrorContentClient(HttpClient http)
{
    private readonly Dictionary<string, object?> _cache = new(StringComparer.Ordinal);

    public Task<MirrorPage?> TryGetNewsAsync(string slug, CancellationToken ct = default) =>
        TryGetAsync<MirrorPage>($"data/news/{SiteUrls.FileName(slug)}.json", ct);

    public Task<MirrorPage?> TryGetDepartmentAsync(string slug, CancellationToken ct = default) =>
        TryGetAsync<MirrorPage>($"data/departments/{SiteUrls.FileName(slug)}.json", ct);

    public Task<MirrorPage?> TryGetPageAsync(MirrorPageRef reference, CancellationToken ct = default) =>
        TryGetAsync<MirrorPage>($"data/pages/{reference.Group}-{reference.Slug}.json", ct);

    public Task<List<ScheduleGroupRef>?> TryGetScheduleGroupsAsync(CancellationToken ct = default) =>
        TryGetAsync<List<ScheduleGroupRef>>("data/schedule-groups.json", ct);

    public Task<ScheduleTable?> TryGetScheduleTableAsync(string file, CancellationToken ct = default) =>
        TryGetAsync<ScheduleTable>($"data/schedule/{file}.json", ct);

    public Task<MirrorMeta?> TryGetMetaAsync(CancellationToken ct = default) =>
        TryGetAsync<MirrorMeta>("data/meta.json", ct);

    public Task<MirrorPage?> TryGetCourseAsync(string slug, CancellationToken ct = default) =>
        TryGetAsync<MirrorPage>($"data/courses/{SiteUrls.FileName(slug)}.json", ct);

    public Task<List<CourseRef>?> TryGetCourseIndexAsync(CancellationToken ct = default) =>
        TryGetAsync<List<CourseRef>>("data/courses.json", ct);

    public Task<EmployeeProfile?> TryGetEmployeeAsync(string slug, CancellationToken ct = default) =>
        TryGetAsync<EmployeeProfile>($"data/employees/{SiteUrls.FileName(slug)}.json", ct);

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
