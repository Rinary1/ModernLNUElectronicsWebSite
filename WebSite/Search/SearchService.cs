using System.Net.Http.Json;
using System.Text.RegularExpressions;
using WebSite.Data;

namespace WebSite.Search;

public sealed partial class SearchService(HttpClient http)
{
    private const string IndexUrl = "data/search-index.json";

    private const int SnippetLength = 220;

    [GeneratedRegex(@"[^\p{L}\p{Nd}']+")]
    private static partial Regex NotWord();

    private readonly SemaphoreSlim _gate = new(1, 1);

    private List<Entry>? _entries;

    public bool IsLoaded => _entries is not null;

    public int DocumentCount => _entries?.Count ?? 0;

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_entries is not null)
            return;

        await _gate.WaitAsync(ct);
        try
        {
            if (_entries is not null)
                return;

            var docs = await TryGetIndexAsync(ct) ?? [];
            _entries = docs.Select(Entry.From).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public SearchResults Query(string? raw, SearchKind? kind = null, int limit = 50)
    {
        if (_entries is null || string.IsNullOrWhiteSpace(raw))
            return SearchResults.Empty;

        var terms = Stems(raw);
        if (terms.Length == 0)
            return SearchResults.Empty;

        var hits = new List<SearchHit>();

        foreach (var entry in _entries)
        {
            double score = 0;
            var allFound = true;

            foreach (var term in terms)
            {
                var part = 0d;

                if (entry.Title.Contains(term))
                    part += 14;

                if (entry.Subtitle.Contains(term))
                    part += 4;

                if (entry.Text.Contains(term))
                    part += 2;

                if (part == 0)
                {
                    allFound = false;
                    break;
                }

                score += part;
            }

            if (!allFound)
                continue;

            hits.Add(new SearchHit(entry.Doc, score, Snippet(entry.Doc.Text, terms)));
        }

        var counts = hits
            .GroupBy(h => h.Doc.Kind)
            .Select(g => new KindCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Kind)
            .ToList();

        var ranked = hits
            .Where(h => kind is null || h.Doc.Kind == kind)
            .OrderByDescending(h => h.Score)
            .ThenByDescending(h => h.Doc.Date ?? DateTime.MinValue)
            .ThenBy(h => h.Doc.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();

        return new SearchResults(ranked, counts, hits.Count);
    }

    public static string[] Stems(string? value) =>
        Words(value).Select(UkrainianStemmer.Stem).Distinct().ToArray();

    public static IEnumerable<string> Words(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : NotWord().Split(Fold(value)).Where(w => w.Length > 0);

    public static string Fold(string value)
    {
        var chars = value.ToLowerInvariant().ToCharArray();

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                'ʼ' or '’' or '‘' => '\'',
                'ё' => 'е',
                var c => c,
            };
        }

        return new string(chars);
    }

    private async Task<List<SearchDoc>?> TryGetIndexAsync(CancellationToken ct)
    {
        try
        {
            return await http.GetFromJsonAsync<List<SearchDoc>>(IndexUrl, ct);
        }
        catch (Exception e) when (e is HttpRequestException or NotSupportedException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string Snippet(string text, string[] terms)
    {
        if (text.Length <= SnippetLength)
            return text;

        var start = Math.Max(0, FirstHitIndex(text, terms) - 80);

        if (start > 0)
        {
            var space = text.IndexOf(' ', start);
            start = space > 0 && space - start < 20 ? space + 1 : start;
        }

        var length = Math.Min(SnippetLength, text.Length - start);
        var fragment = text.Substring(start, length).Trim();

        return (start > 0 ? "..." : string.Empty) + fragment + (start + length < text.Length ? "..." : string.Empty);
    }

    private static int FirstHitIndex(string text, string[] terms)
    {
        var folded = Fold(text);
        var best = -1;

        foreach (var term in terms)
        {
            var index = folded.IndexOf(term, StringComparison.Ordinal);
            if (index >= 0 && (best < 0 || index < best))
                best = index;
        }

        return best < 0 ? 0 : best;
    }

    private sealed record Entry(SearchDoc Doc, HashSet<string> Title, HashSet<string> Subtitle, HashSet<string> Text)
    {
        public static Entry From(SearchDoc doc) => new(
            doc,
            [.. Stems(doc.Title)],
            [.. Stems(doc.Subtitle)],
            [.. Stems(doc.Text)]);
    }
}
