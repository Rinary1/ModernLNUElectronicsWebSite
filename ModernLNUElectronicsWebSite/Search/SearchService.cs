using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ModernLNUElectronicsWebSite.Scraping;

namespace ModernLNUElectronicsWebSite.Search;

public sealed class SearchService(HttpClient http)
{
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<SearchDoc>? _docs;

    public bool IsLoaded => _docs is not null;

    public int DocumentCount => _docs?.Count ?? 0;

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_docs is not null)
            return;

        await _gate.WaitAsync(ct);
        try
        {
            if (_docs is not null)
                return;

            var docs = new List<SearchDoc>();
            docs.AddRange(await LoadNewsAsync(ct));
            docs.AddRange(await LoadStaffAsync(ct));
            docs.AddRange(await LoadAdministrationAsync(ct));

            _docs = docs
                .GroupBy(d => (d.Kind, d.Url))
                .Select(g => g.First())
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<SearchHit> Query(string? raw, int limit = 50)
    {
        if (_docs is null || string.IsNullOrWhiteSpace(raw))
            return Array.Empty<SearchHit>();

        var tokens = Normalize(raw)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .ToArray();

        if (tokens.Length == 0)
            return Array.Empty<SearchHit>();

        var hits = new List<SearchHit>();

        foreach (var doc in _docs)
        {
            var title = Normalize(doc.Title);
            var subtitle = Normalize(doc.Subtitle ?? string.Empty);
            var text = Normalize(doc.Text);

            double score = 0;
            var allTokensFound = true;

            foreach (var token in tokens)
            {
                double part = 0;

                var inTitle = title.IndexOf(token, StringComparison.Ordinal);
                if (inTitle >= 0)
                    part += IsWordBoundary(title, inTitle) ? 14 : 10;

                if (subtitle.Contains(token, StringComparison.Ordinal))
                    part += 4;

                if (text.Contains(token, StringComparison.Ordinal))
                    part += 2;

                if (part == 0)
                {
                    allTokensFound = false;
                    break;
                }

                score += part;
            }

            if (!allTokensFound)
                continue;

            hits.Add(new SearchHit(doc, score, BuildSnippet(doc.Text, text, tokens)));
        }

        return hits
            .OrderByDescending(h => h.Score)
            .ThenByDescending(h => h.Doc.Date ?? DateTime.MinValue)
            .ThenBy(h => h.Doc.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private async Task<IEnumerable<SearchDoc>> LoadNewsAsync(CancellationToken ct)
    {
        var items = await TryGetAsync<List<NewsItem>>("data/news.json", ct);
        return items is null
            ? Enumerable.Empty<SearchDoc>()
            : items.Select(n => new SearchDoc(
                Id: $"news:{n.Url}",
                Kind: SearchKind.News,
                Title: n.Title,
                Url: n.Url,
                Subtitle: n.PublishedAt?.ToString("dd.MM.yyyy") ?? n.RawDate,
                Text: n.Excerpt ?? n.Title,
                Date: n.PublishedAt));
    }

    private async Task<IEnumerable<SearchDoc>> LoadStaffAsync(CancellationToken ct)
    {
        var items = await TryGetAsync<List<StaffItem>>("data/staff.json", ct);
        return items is null
            ? Enumerable.Empty<SearchDoc>()
            : items.Select(s => new SearchDoc(
                Id: $"staff:{s.ProfileUrl}",
                Kind: SearchKind.Staff,
                Title: s.FullName,
                Url: s.ProfileUrl,
                Subtitle: $"{s.Group.Title} · {s.Position}",
                Text: $"{s.FullName} {s.Position} {s.Group.Title} {s.Email}",
                Date: null));
    }

    private async Task<IEnumerable<SearchDoc>> LoadAdministrationAsync(CancellationToken ct)
    {
        var items = await TryGetAsync<List<AdministrationPerson>>("data/administration.json", ct);
        return items is null
            ? Enumerable.Empty<SearchDoc>()
            : items.Select(a => new SearchDoc(
                Id: $"adm:{a.ProfileUrl ?? a.Name}",
                Kind: SearchKind.Administration,
                Title: a.Name,
                Url: a.ProfileUrl ?? "administration",
                Subtitle: a.Rank is { Length: > 0 } r ? $"{a.Role} · {r}" : a.Role,
                Text: $"{a.Name} {a.Role} {a.RoleDetail} {a.Rank}",
                Date: null));
    }

    private async Task<T?> TryGetAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            return await http.GetFromJsonAsync<T>(url, ct);
        }
        catch (HttpRequestException)
        {
            return default;
        }
    }

    public static string Normalize(string value) => WhitespaceRun.Replace(
        value.ToLowerInvariant()
            .Replace('ʼ', '\'')
            .Replace('’', '\'') 
            .Replace('‘', '\''),
        " ").Trim();

    private static bool IsWordBoundary(string haystack, int index) =>
        index == 0 || !char.IsLetterOrDigit(haystack[index - 1]);

    private static string BuildSnippet(string original, string normalized, IEnumerable<string> tokens)
    {
        var first = tokens
            .Select(t => normalized.IndexOf(t, StringComparison.Ordinal))
            .Where(i => i >= 0)
            .DefaultIfEmpty(0)
            .Min();

        var start = Math.Max(0, Math.Min(first, original.Length) - 80);
        var length = Math.Min(220, original.Length - start);
        if (length <= 0)
            return original;

        var fragment = original.Substring(start, length).Trim();
        return (start > 0 ? "..." : string.Empty) + fragment + (start + length < original.Length ? "..." : string.Empty);
    }
}
