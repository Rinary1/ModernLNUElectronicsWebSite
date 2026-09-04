using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ModernLNUElectronicsWebSite.Content;
using ModernLNUElectronicsWebSite.Scraping;

namespace ModernLNUElectronicsWebSite.Search;

public sealed class SearchService(HttpClient http, MirrorContentClient content)
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

            var sources = await Task.WhenAll(
                LoadNewsAsync(ct),
                LoadStaffAsync(ct),
                LoadAdministrationAsync(ct),
                LoadPartnersAsync(ct),
                LoadScheduleAsync(ct),
                LoadMirroredPagesAsync(ct));

            var docs = new List<SearchDoc>(StaticPages());
            foreach (var source in sources)
                docs.AddRange(source);

            _docs = docs
                .GroupBy(d => d.Id)
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

            hits.Add(new SearchHit(doc, score, BuildSnippet(doc.Text, tokens)));
        }

        return hits
            .OrderByDescending(h => h.Score)
            .ThenByDescending(h => h.Doc.Date ?? DateTime.MinValue)
            .ThenBy(h => h.Doc.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static IEnumerable<SearchDoc> StaticPages()
    {
        var pages = new (string Route, string Title, string Subtitle, string Text, string? Source)[]
        {
            ("", "Головна", "Розділ дзеркала", "головна новини факультет електроніки", $"{SiteUrls.Origin}/"),
            ("about", "Про факультет", "Розділ дзеркала", "про факультет партнери історія",
                $"{SiteUrls.Origin}/about/introduction/"),
            ("departments", "Кафедри", "Розділ дзеркала", "кафедри підрозділи лабораторії склад",
                $"{SiteUrls.Origin}/about/departments/"),
            ("staff", "Співробітники", "Розділ дзеркала", "співробітники викладачі персонал",
                $"{SiteUrls.Origin}/about/staff/"),
            ("administration", "Адміністрація", "Розділ дзеркала", "деканат рада факультету адміністрація",
                $"{SiteUrls.Origin}/about/administration/"),
            ("news", "Новини та події", "Розділ дзеркала", "новини події анонси", $"{SiteUrls.Origin}/news/"),
            ("schedule", "Розклад", "Розділ дзеркала", "розклад занять іспити заліки пари сесія",
                $"{SiteUrls.Origin}/students/career/"),
            ("applicants", "Абітурієнту", "Розділ дзеркала", "вступ абітурієнт спеціальності освітні програми",
                $"{SiteUrls.Origin}/admission/your-prospects/"),
            ("science", "Наука", "Розділ дзеркала", "наука дослідження конференції публікації",
                $"{SiteUrls.Origin}/research/research-areas/"),
            ("contacts", "Контакти", "Розділ дзеркала", "контакти адреса телефон пошта як дістатися", null),
        };

        return pages.Select(p => new SearchDoc(
            Id: $"page:{p.Route}",
            Kind: SearchKind.Page,
            Title: p.Title,
            Route: p.Route,
            SourceUrl: p.Source,
            Subtitle: p.Subtitle,
            Text: p.Text,
            Date: null));
    }

    private async Task<IEnumerable<SearchDoc>> LoadNewsAsync(CancellationToken ct)
    {
        var items = await TryGetAsync<List<NewsItem>>("data/news.json", ct);
        return items is null
            ? Enumerable.Empty<SearchDoc>()
            : items.Select(n => new SearchDoc(
                Id: $"news:{n.Slug}",
                Kind: SearchKind.News,
                Title: n.Title,
                Route: $"news/{n.Slug}",
                SourceUrl: n.Url,
                Subtitle: n.PublishedAt?.ToString("dd.MM.yyyy") ?? n.RawDate,
                Text: n.Excerpt ?? n.Title,
                Date: n.PublishedAt));
    }

    private async Task<IEnumerable<SearchDoc>> LoadStaffAsync(CancellationToken ct)
    {
        var items = await TryGetAsync<List<StaffItem>>("data/staff.json", ct);
        if (items is null)
            return Enumerable.Empty<SearchDoc>();

        var people = items.Select(s => new SearchDoc(
            Id: $"staff:{SiteUrls.Slug(s.ProfileUrl)}",
            Kind: SearchKind.Staff,
            Title: s.FullName,
            Route: $"staff/{SiteUrls.Slug(s.ProfileUrl)}",
            SourceUrl: s.ProfileUrl,
            Subtitle: $"{s.Group.Title} · {s.Position}",
            Text: $"{s.FullName} {s.Position} {s.Group.Title} {s.Email}",
            Date: null));

        var departments = items
            .Where(s => s.Group.Url is not null)
            .GroupBy(s => s.Group.Url!)
            .Select(g => new SearchDoc(
                Id: $"department:{SiteUrls.Slug(g.Key)}",
                Kind: SearchKind.Department,
                Title: g.First().Group.Title,
                Route: $"departments/{SiteUrls.Slug(g.Key)}",
                SourceUrl: g.Key,
                Subtitle: $"Підрозділ · {g.Count()} співробітників",
                Text: $"{g.First().Group.Title} кафедра підрозділ лабораторія",
                Date: null));

        return people.Concat(departments);
    }

    private async Task<IEnumerable<SearchDoc>> LoadAdministrationAsync(CancellationToken ct)
    {
        var items = await TryGetAsync<List<AdministrationPerson>>("data/administration.json", ct);
        return items is null
            ? Enumerable.Empty<SearchDoc>()
            : items.Select(a => new SearchDoc(
                Id: $"adm:{a.Name}:{a.Role}",
                Kind: SearchKind.Administration,
                Title: a.Name,
                Route: a.ProfileUrl is not null ? $"staff/{SiteUrls.Slug(a.ProfileUrl)}" : "administration",
                SourceUrl: a.ProfileUrl ?? $"{SiteUrls.Origin}/about/administration/",
                Subtitle: a.Rank is { Length: > 0 } r ? $"{a.Role} · {r}" : a.Role,
                Text: $"{a.Name} {a.Role} {a.RoleDetail} {a.Rank}",
                Date: null));
    }

    private async Task<IEnumerable<SearchDoc>> LoadPartnersAsync(CancellationToken ct)
    {
        var items = await TryGetAsync<List<Partner>>("data/partners.json", ct);
        return items is null
            ? Enumerable.Empty<SearchDoc>()
            : items.Select(p => new SearchDoc(
                Id: $"partner:{p.Name}",
                Kind: SearchKind.Partner,
                Title: p.Name,
                Route: "about",
                SourceUrl: p.Url,
                Subtitle: "Партнер факультету",
                Text: $"{p.Name} {p.Description}",
                Date: null));
    }

    private async Task<IEnumerable<SearchDoc>> LoadScheduleAsync(CancellationToken ct)
    {
        var items = await TryGetAsync<List<ScheduleDoc>>("data/schedule.json", ct);
        return items is null
            ? Enumerable.Empty<SearchDoc>()
            : items.Select(d => new SearchDoc(
                Id: $"schedule:{d.Url}",
                Kind: SearchKind.Schedule,
                Title: d.Title,
                Route: $"schedule?doc={Uri.EscapeDataString(d.Url)}",
                SourceUrl: d.SourceUrl,
                Subtitle: $"{CategoryLabel(d.Category)} · {d.Section}",
                Text: $"{d.Title} {d.Section} розклад pdf",
                Date: null));
    }

    private async Task<IEnumerable<SearchDoc>> LoadMirroredPagesAsync(CancellationToken ct)
    {
        var pages = await Task.WhenAll(MirrorCatalog.Pages.Select(async reference =>
            (Reference: reference, Page: await content.TryGetPageAsync(reference, ct))));

        return pages
            .Where(p => p.Page is not null)
            .Select(p => new SearchDoc(
                Id: $"mirror:{p.Reference.Group}/{p.Reference.Slug}",
                Kind: SearchKind.Page,
                Title: p.Reference.Title,
                Route: $"{p.Reference.Group}/{p.Reference.Slug}",
                SourceUrl: p.Reference.SourceUrl,
                Subtitle: GroupLabel(p.Reference.Group),
                Text: p.Page!.PlainText,
                Date: p.Page.PublishedAt));
    }

    private static string CategoryLabel(ScheduleCategory category) => category switch
    {
        ScheduleCategory.Classes => "Розклад занять",
        ScheduleCategory.Exams => "Сесія",
        _ => "Розклад",
    };

    private static string GroupLabel(string group) => group switch
    {
        MirrorCatalog.Applicants => "Абітурієнту",
        MirrorCatalog.Science => "Наука",
        _ => group,
    };

    private async Task<T?> TryGetAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            return await http.GetFromJsonAsync<T>(url, ct);
        }
        catch (Exception e) when (e is HttpRequestException or NotSupportedException or System.Text.Json.JsonException)
        {
            return default;
        }
    }

    public static string Normalize(string value) => WhitespaceRun.Replace(Fold(value), " ").Trim();

    public static string Fold(string value)
    {
        var chars = value.ToLowerInvariant().ToCharArray();

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                'ʼ' or '’' or '‘' => '\'',
                var c when char.IsWhiteSpace(c) => ' ',
                var c => c,
            };
        }

        return new string(chars);
    }

    private static bool IsWordBoundary(string haystack, int index) =>
        index == 0 || !char.IsLetterOrDigit(haystack[index - 1]);

    private static string BuildSnippet(string original, IEnumerable<string> tokens)
    {
        var normalized = Fold(original);

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
