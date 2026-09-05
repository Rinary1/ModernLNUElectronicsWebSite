using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModernLNUElectronicsWebSite.Data;
using ModernLNUElectronicsWebSite.Scraper;
using ModernLNUElectronicsWebSite.Scraper.Scraping;

var options = ScraperOptions.Parse(args);

Console.WriteLine($"Каталог даних: {Path.GetFullPath(options.OutDir)}");
Console.WriteLine(options.Refresh
    ? "Режим: повне перезавантаження"
    : "Режим: доповнення (наявні статті та профілі не перезавантажуються)");

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd(
    "ModernLNUElectronicsMirror/1.0 (+https://github.com/OWNER/REPO; scheduled scraper)");

var htmlSource = new HttpHtmlSource(http);
var articles = new ArticleScraper(htmlSource);
var store = new JsonStore(options.OutDir);

if (options.IndexOnly)
{
    await EnrichStaffPhotosAsync();
    await BuildSearchIndexAsync();
    Console.WriteLine("Готово (лише похідні файли).");
    return;
}

if (options.PagesOnly)
{
    await ScrapeMirroredPagesAsync();
    await BuildSearchIndexAsync();
    Console.WriteLine("Готово (лише розділи каталогу).");
    return;
}

if (options.CoursesOnly)
{
    await ScrapeCoursesAsync(await store.ReadAsync<List<StaffItem>>("staff.json") ?? []);
    await BuildSearchIndexAsync();
    Console.WriteLine("Готово (лише дисципліни).");
    return;
}

var newsCount = await ScrapeNewsAsync();
var staff = await ScrapeStaffAsync();
await ScrapeAdministrationAsync();
await ScrapePartnersAsync();
var scheduleCount = await ScrapeScheduleAsync();
await ScrapeMirroredPagesAsync();
var departmentCount = await ScrapeDepartmentsAsync(staff);
var profileCount = await ScrapeEmployeesAsync(staff);
var courseCount = await ScrapeCoursesAsync(staff);

await EnrichStaffPhotosAsync();
await BuildSearchIndexAsync();

await store.WriteAsync("meta.json", new MirrorMeta
{
    GeneratedAt = DateTime.UtcNow,
    NewsCount = newsCount,
    StaffCount = staff.Count,
    EmployeeProfileCount = profileCount,
    DepartmentCount = departmentCount,
    ScheduleDocCount = scheduleCount,
    CourseCount = courseCount,
});

Console.WriteLine("Готово.");
return;

async Task<int> ScrapeNewsAsync()
{
    var scraper = new NewsScraper(htmlSource);
    var collected = new List<NewsItem>();
    string? pageUrl = $"{SiteUrls.Origin}/news/";

    for (var page = 1; page <= options.NewsPages && pageUrl is not null; page++)
    {
        Console.WriteLine($"news [{page}/{options.NewsPages}] {pageUrl}");
        var result = await scraper.LoadPageAsync(pageUrl);
        collected.AddRange(result.Items);
        pageUrl = result.NextPageUrl;

        if (pageUrl is not null)
            await Delay();
    }

    var archive = await store.ReadAsync<List<NewsItem>>("news.json") ?? [];
    var byUrl = archive.ToDictionary(i => i.Url, StringComparer.Ordinal);
    var added = 0;

    foreach (var item in collected)
    {
        if (byUrl.TryGetValue(item.Url, out var known))
        {
            byUrl[item.Url] = item with { CoverImageUrl = item.CoverImageUrl ?? known.CoverImageUrl };
            continue;
        }

        byUrl[item.Url] = item;
        added++;
    }

    Console.WriteLine($"  -> нових {added}, в архіві було {archive.Count}");

    await MirrorEachAsync("news", byUrl.Values.Select(i => (i.Slug, i.Url)));

    var index = new List<NewsItem>();
    foreach (var item in byUrl.Values.OrderByDescending(i => i.PublishedAt ?? DateTime.MinValue))
        index.Add(item with { CoverImageUrl = item.CoverImageUrl ?? await CoverOfAsync(item.Slug) });

    await WriteListAsync("news.json", index);
    Console.WriteLine($"  -> {index.Count} новин у стрічці, з обкладинками {index.Count(i => i.CoverImageUrl is not null)}");

    return index.Count;
}

async Task<int> ScrapeCoursesAsync(List<StaffItem> staffList)
{
    var urls = new Dictionary<string, string>(StringComparer.Ordinal);
    var linkTitles = new Dictionary<string, string>(StringComparer.Ordinal);
    var lecturers = new Dictionary<string, List<CourseLecturer>>(StringComparer.Ordinal);

    foreach (var person in staffList.DistinctBy(s => s.ProfileUrl))
    {
        var personSlug = SiteUrls.Slug(person.ProfileUrl);
        var profile = await store.ReadAsync<EmployeeProfile>($"employees/{SiteUrls.FileName(personSlug)}.json");
        if (profile is null)
            continue;

        foreach (var course in profile.Courses)
        {
            if (!SiteUrls.IsOriginalSite(course.Url) || SiteUrls.Kind(course.Url) is not "course")
                continue;

            var slug = SiteUrls.Slug(course.Url);
            if (slug.Length == 0)
                continue;

            urls.TryAdd(slug, course.Url);
            linkTitles.TryAdd(slug, course.Title);

            if (!lecturers.TryGetValue(slug, out var people))
                lecturers[slug] = people = [];

            if (people.All(l => l.Slug != personSlug))
                people.Add(new CourseLecturer(person.FullName, personSlug));
        }
    }

    Console.WriteLine($"courses -> знайдено {urls.Count} дисциплін у профілях");

    var fromText = await CollectCourseLinksAsync(urls);
    Console.WriteLine($"  -> ще {fromText} з навчальних планів і сторінок кафедр");

    await MirrorEachAsync("courses", urls.Select(kv => (kv.Key, kv.Value)));

    var index = new List<CourseRef>(urls.Count);

    foreach (var (slug, url) in urls)
    {
        var page = await store.ReadAsync<MirrorPage>($"courses/{SiteUrls.FileName(slug)}.json");

        index.Add(new CourseRef
        {
            Slug = slug,
            Title = page?.Title is { Length: > 0 } title
                ? title
                : linkTitles.TryGetValue(slug, out var linkTitle) ? linkTitle : slug,
            SourceUrl = url,
            Lecturers = lecturers.TryGetValue(slug, out var people) ? people : [],
        });
    }

    index.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));

    await WriteListAsync("courses.json", index);
    Console.WriteLine($"  -> у каталозі {index.Count} дисциплін");

    return index.Count;
}

/// Джерело час від часу міняє верстку, і тоді парсер тихо повертає порожньо.
/// Якщо це записати, workflow закомітить порожні дані й задеплоїть їх - сайт
/// стане порожнім, а дізнаємось ми про це від студентів. Тому список, який
/// раптово схуд удвічі, не перезаписуємо і валимо запуск, щоб Action почервонів.
async Task WriteListAsync<T>(string relativePath, IReadOnlyList<T> items)
{
    var existing = (await store.ReadAsync<List<T>>(relativePath))?.Count ?? 0;

    if (!options.AllowShrink && existing > 0 && items.Count * 2 < existing)
    {
        Console.Error.WriteLine(
            $"! {relativePath}: було {existing}, стало {items.Count}. Схоже на зламаний парсер - " +
            "файл лишаю як є. Якщо джерело справді так змінилося, перезапустіть з --allow-shrink.");

        Environment.ExitCode = 1;
        return;
    }

    await store.WriteAsync(relativePath, items);
}

async Task<int> CollectCourseLinksAsync(IDictionary<string, string> urls)
{
    var added = 0;

    foreach (var folder in new[] { "employees", "departments", "pages", "news", "courses" })
    {
        foreach (var file in store.FilesIn(folder))
        {
            var raw = await File.ReadAllTextAsync(file);

            foreach (Match match in CourseLinks.Href.Matches(raw))
            {
                var slug = match.Groups["slug"].Value;

                if (slug.Length > 0 && urls.TryAdd(slug, $"{SiteUrls.Origin}/course/{slug}/"))
                    added++;
            }
        }
    }

    return added;
}

async Task EnrichStaffPhotosAsync()
{
    var staffList = await store.ReadAsync<List<StaffItem>>("staff.json");
    if (staffList is null)
        return;

    var photos = new Dictionary<string, string?>(StringComparer.Ordinal);
    var updated = new List<StaffItem>(staffList.Count);

    foreach (var person in staffList)
    {
        var slug = SiteUrls.Slug(person.ProfileUrl);

        if (!photos.TryGetValue(slug, out var photo))
        {
            photo = (await store.ReadAsync<EmployeeProfile>($"employees/{SiteUrls.FileName(slug)}.json"))?.PhotoUrl;
            photos[slug] = photo;
        }

        updated.Add(person with { PhotoUrl = photo });
    }

    await store.WriteAsync("staff.json", updated);
    Console.WriteLine($"staff -> фото у {updated.Count(p => p.PhotoUrl is not null)} із {updated.Count}");
}

async Task BuildSearchIndexAsync()
{
    var docs = new List<SearchDoc>(SearchIndexBuilder.StaticPages());

    foreach (var item in await store.ReadAsync<List<NewsItem>>("news.json") ?? [])
    {
        var page = await store.ReadAsync<MirrorPage>($"news/{SiteUrls.FileName(item.Slug)}.json");

        docs.Add(new SearchDoc(
            Id: $"news:{item.Slug}",
            Kind: SearchKind.News,
            Title: item.Title,
            Route: $"news/{item.Slug}",
            SourceUrl: item.Url,
            Subtitle: item.PublishedAt?.ToString("dd.MM.yyyy") ?? item.RawDate,
            Text: SearchIndexBuilder.Plain(page?.PlainText ?? item.Excerpt ?? item.Title),
            Date: item.PublishedAt));
    }

    var staffList = await store.ReadAsync<List<StaffItem>>("staff.json") ?? [];

    foreach (var person in staffList.DistinctBy(s => s.ProfileUrl))
    {
        var slug = SiteUrls.Slug(person.ProfileUrl);
        var profile = await store.ReadAsync<EmployeeProfile>($"employees/{SiteUrls.FileName(slug)}.json");

        var body = profile is null
            ? string.Empty
            : SearchIndexBuilder.Join(
                profile.PositionText,
                profile.AcademicDegree,
                profile.AcademicTitle,
                profile.ResearchInterests,
                string.Join(' ', profile.Courses.Select(c => c.Title)),
                string.Join(' ', profile.Sections.Select(x => SearchIndexBuilder.Plain(x.Html))));

        docs.Add(new SearchDoc(
            Id: $"staff:{slug}",
            Kind: SearchKind.Staff,
            Title: person.FullName,
            Route: $"staff/{slug}",
            SourceUrl: person.ProfileUrl,
            Subtitle: $"{person.Group.Title} · {person.Position}",
            Text: SearchIndexBuilder.Plain(SearchIndexBuilder.Join(
                person.FullName, person.Position, person.Group.Title, person.Email, body)),
            Date: null));
    }

    foreach (var group in staffList.Where(s => s.Group.Url is not null).GroupBy(s => s.Group.Url!))
    {
        var slug = SiteUrls.Slug(group.Key);
        var page = await store.ReadAsync<MirrorPage>($"departments/{SiteUrls.FileName(slug)}.json");

        docs.Add(new SearchDoc(
            Id: $"department:{slug}",
            Kind: SearchKind.Department,
            Title: group.First().Group.Title,
            Route: $"departments/{slug}",
            SourceUrl: group.Key,
            Subtitle: $"Підрозділ · {group.Count()} співробітників",
            Text: SearchIndexBuilder.Plain(SearchIndexBuilder.Join(
                group.First().Group.Title,
                "кафедра підрозділ лабораторія",
                page?.PlainText,
                string.Join(' ', group.Select(s => s.FullName)))),
            Date: null));
    }

    foreach (var person in await store.ReadAsync<List<AdministrationPerson>>("administration.json") ?? [])
    {
        docs.Add(new SearchDoc(
            Id: $"adm:{person.Name}:{person.Role}",
            Kind: SearchKind.Administration,
            Title: person.Name,
            Route: person.ProfileUrl is not null ? $"staff/{SiteUrls.Slug(person.ProfileUrl)}" : "administration",
            SourceUrl: person.ProfileUrl ?? $"{SiteUrls.Origin}/about/administration/",
            Subtitle: person.Rank is { Length: > 0 } rank ? $"{person.Role} · {rank}" : person.Role,
            Text: SearchIndexBuilder.Join(person.Name, person.Role, person.RoleDetail, person.Rank),
            Date: null));
    }

    foreach (var partner in await store.ReadAsync<List<Partner>>("partners.json") ?? [])
    {
        docs.Add(new SearchDoc(
            Id: $"partner:{partner.Name}",
            Kind: SearchKind.Partner,
            Title: partner.Name,
            Route: "about",
            SourceUrl: partner.Url,
            Subtitle: "Партнер факультету",
            Text: SearchIndexBuilder.Plain(SearchIndexBuilder.Join(partner.Name, partner.Description)),
            Date: null));
    }

    foreach (var doc in await store.ReadAsync<List<ScheduleDoc>>("schedule.json") ?? [])
    {
        docs.Add(new SearchDoc(
            Id: $"schedule:{doc.Url}",
            Kind: SearchKind.Schedule,
            Title: doc.Title,
            Route: $"schedule?doc={Uri.EscapeDataString(doc.Url)}",
            SourceUrl: doc.SourceUrl,
            Subtitle: $"{(doc.Category == ScheduleCategory.Exams ? "Сесія" : "Розклад занять")} · {doc.Section}",
            Text: SearchIndexBuilder.Join(doc.Title, doc.Section, "розклад pdf"),
            Date: null));
    }

    // Розібрані PDF: шукати за прізвищем викладача чи предметом і потрапляти
    // одразу на розклад своєї групи - те, заради чого це все й робилося.
    var tables = new Dictionary<string, ScheduleTable>(StringComparer.Ordinal);

    foreach (var reference in await store.ReadAsync<List<ScheduleGroupRef>>("schedule-groups.json") ?? [])
    {
        if (!tables.TryGetValue(reference.File, out var table))
        {
            var loaded = await store.ReadAsync<ScheduleTable>($"schedule/{reference.File}.json");
            if (loaded is null)
                continue;

            tables[reference.File] = table = loaded;
        }

        var cells = table.Rows
            .Select(r => r.CellOf(reference.Column))
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal);

        docs.Add(new SearchDoc(
            Id: $"group:{reference.Group}:{reference.File}",
            Kind: SearchKind.Schedule,
            Title: $"Розклад {reference.Group}",
            Route: $"schedule?group={Uri.EscapeDataString(reference.Group)}",
            SourceUrl: table.Url,
            Subtitle: $"{(reference.IsWeekly ? "Заняття" : "Сесія")} · {reference.Title}",
            Text: SearchIndexBuilder.Plain(SearchIndexBuilder.Join(
                reference.Group, reference.Title, reference.Section, string.Join(' ', cells))),
            Date: null));
    }

    foreach (var reference in MirrorCatalog.Pages)
    {
        var page = await store.ReadAsync<MirrorPage>($"pages/{reference.Group}-{reference.Slug}.json");
        if (page is null)
            continue;

        docs.Add(new SearchDoc(
            Id: $"mirror:{reference.Group}/{reference.Slug}",
            Kind: SearchKind.Page,
            Title: reference.Title,
            Route: $"{reference.Group}/{reference.Slug}",
            SourceUrl: reference.SourceUrl,
            Subtitle: MirrorCatalog.LabelOf(reference.Group),
            Text: SearchIndexBuilder.Plain(page.PlainText),
            Date: page.PublishedAt));
    }

    foreach (var course in await store.ReadAsync<List<CourseRef>>("courses.json") ?? [])
    {
        var page = await store.ReadAsync<MirrorPage>($"courses/{SiteUrls.FileName(course.Slug)}.json");

        docs.Add(new SearchDoc(
            Id: $"course:{course.Slug}",
            Kind: SearchKind.Course,
            Title: course.Title,
            Route: $"courses/{course.Slug}",
            SourceUrl: course.SourceUrl,
            Subtitle: course.Lecturers.Count > 0
                ? $"Дисципліна · {string.Join(", ", course.Lecturers.Take(2).Select(l => l.Name))}"
                : "Навчальна дисципліна",
            Text: SearchIndexBuilder.Plain(page?.PlainText, SearchIndexBuilder.MaxCourseText),
            Date: null));
    }

    var index = docs.GroupBy(d => d.Id).Select(g => g.First()).ToList();
    await store.WriteAsync("search-index.json", index, indented: false);
    Console.WriteLine($"search-index -> {index.Count} записів, " +
        $"{index.Sum(d => d.Text.Length) / 1024} КБ тексту");
}

async Task<string?> CoverOfAsync(string slug) =>
    (await store.ReadAsync<MirrorPage>($"news/{SiteUrls.FileName(slug)}.json"))?.CoverImageUrl;

async Task<List<StaffItem>> ScrapeStaffAsync()
{
    var staffList = await new StaffScraper(htmlSource).LoadPageAsync($"{SiteUrls.Origin}/about/staff/");
    await WriteListAsync("staff.json", staffList);
    Console.WriteLine($"staff -> {staffList.Count} осіб, підрозділів: " +
        $"{staffList.Select(s => s.Group.Title).Distinct().Count()}");

    return staffList.ToList();
}

async Task ScrapeAdministrationAsync()
{
    var people = await new AdministrationScraper(htmlSource).LoadPageAsync($"{SiteUrls.Origin}/about/administration/");
    await WriteListAsync("administration.json", people);
    Console.WriteLine($"administration -> {people.Count} " +
        $"(рада: {people.Count(p => p.Section == AdministrationSection.Council)})");
}

async Task ScrapePartnersAsync()
{
    var partners = await new PartnersScraper(htmlSource).LoadPageAsync($"{SiteUrls.Origin}/about/introduction/");
    await WriteListAsync("partners.json", partners);
    Console.WriteLine($"partners -> {partners.Count}");
}

async Task<int> ScrapeScheduleAsync()
{
    var scraper = new SchedulePdfScraper(htmlSource);
    var docs = new List<ScheduleDoc>();

    var sources = new (string Url, ScheduleCategory Category)[]
    {
        ($"{SiteUrls.Origin}/students/career/", ScheduleCategory.Classes),
        ($"{SiteUrls.Origin}/students/rozklad-format-pdf/", ScheduleCategory.Exams),
    };

    foreach (var (url, category) in sources)
    {
        var found = await scraper.LoadPageAsync(url, category);
        docs.AddRange(found);
        Console.WriteLine($"schedule [{category}] -> {found.Count} PDF");
        await Delay();
    }

    await WriteListAsync("schedule.json", docs);
    return docs.Count;
}

async Task ScrapeMirroredPagesAsync()
{
    foreach (var page in MirrorCatalog.Pages)
    {
        var path = $"pages/{page.Group}-{page.Slug}.json";
        if (!options.Refresh && store.Exists(path))
            continue;

        Console.WriteLine($"page [{page.Group}] {page.SourceUrl}");
        await TryMirrorAsync(page.SourceUrl, path, page.Title);
        await Delay();
    }

    Console.WriteLine($"pages -> {MirrorCatalog.Pages.Count} розділів");
}

async Task<int> ScrapeDepartmentsAsync(List<StaffItem> staffList)
{
    var urls = staffList
        .Select(s => s.Group.Url)
        .OfType<string>()
        .Distinct()
        .ToList();

    await MirrorEachAsync("departments", urls.Select(u => (SiteUrls.Slug(u), u)));
    Console.WriteLine($"departments -> {urls.Count} сторінок підрозділів");
    return urls.Count;
}

async Task<int> ScrapeEmployeesAsync(List<StaffItem> staffList)
{
    if (options.SkipProfiles)
    {
        Console.WriteLine("employees -> пропущено (--skip-profiles)");
        return store.Count("employees");
    }

    var scraper = new EmployeeScraper(htmlSource);
    var urls = staffList.Select(s => s.ProfileUrl).Distinct().ToList();
    var written = 0;

    foreach (var url in urls)
    {
        var path = $"employees/{SiteUrls.FileName(SiteUrls.Slug(url))}.json";
        if (!options.Refresh && store.Exists(path))
            continue;

        Console.WriteLine($"employee [{written + 1}] {url}");
        try
        {
            await store.WriteAsync(path, await scraper.LoadAsync(url));
            written++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ! пропущено: {ex.Message}");
        }

        await Delay();
    }

    Console.WriteLine($"employees -> завантажено {written}, всього в списку {urls.Count}");
    return store.Count("employees");
}

async Task MirrorEachAsync(string folder, IEnumerable<(string Slug, string Url)> items)
{
    var written = 0;

    foreach (var (slug, url) in items)
    {
        if (slug.Length == 0)
            continue;

        var path = $"{folder}/{SiteUrls.FileName(slug)}.json";
        if (!options.Refresh && store.Exists(path))
            continue;

        Console.WriteLine($"{folder} [{written + 1}] {url}");
        if (await TryMirrorAsync(url, path))
            written++;

        await Delay();
    }

    Console.WriteLine($"  -> {folder}: завантажено {written}");
}

async Task<bool> TryMirrorAsync(string url, string path, string? titleOverride = null)
{
    try
    {
        var page = await articles.LoadAsync(url);
        if (titleOverride is not null)
            page = page with { Title = titleOverride };

        await store.WriteAsync(path, page);
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ! пропущено {url}: {ex.Message}");
        return false;
    }
}

Task Delay() => Task.Delay(options.DelayMs);

file sealed class JsonStore(string root)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions Compact = new(Options) { WriteIndented = false };

    public bool Exists(string relativePath) => File.Exists(Path.Combine(root, relativePath));

    public IEnumerable<string> FilesIn(string folder)
    {
        var directory = Path.Combine(root, folder);
        return Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.json") : [];
    }

    public int Count(string folder)
    {
        var path = Path.Combine(root, folder);
        return Directory.Exists(path) ? Directory.EnumerateFiles(path, "*.json").Count() : 0;
    }

    public async Task<T?> ReadAsync<T>(string relativePath)
    {
        var full = Path.Combine(root, relativePath);
        if (!File.Exists(full))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(full), Options);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"  ! не вдалося прочитати {relativePath}: {ex.Message}");
            return default;
        }
    }

    public async Task WriteAsync<T>(string relativePath, T value, bool indented = true)
    {
        var full = Path.GetFullPath(Path.Combine(root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(
            full, JsonSerializer.Serialize(value, indented ? Options : Compact) + Environment.NewLine);
    }
}

file sealed record ScraperOptions(
    string OutDir, int NewsPages, bool SkipProfiles, bool Refresh, bool IndexOnly, bool PagesOnly, bool CoursesOnly, bool AllowShrink, int DelayMs)
{
    public static ScraperOptions Parse(string[] args)
    {
        var outDir = Path.Combine("ModernLNUElectronicsWebSite", "wwwroot", "data");
        var newsPages = 3;
        var skipProfiles = false;
        var refresh = false;
        var indexOnly = false;
        var pagesOnly = false;
        var coursesOnly = false;
        var allowShrink = false;
        var delayMs = 1000;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    outDir = args[++i];
                    break;

                case "--news-pages" when i + 1 < args.Length && int.TryParse(args[i + 1], out var pages):
                    newsPages = Math.Max(1, pages);
                    i++;
                    break;

                case "--delay" when i + 1 < args.Length && int.TryParse(args[i + 1], out var delay):
                    delayMs = Math.Max(0, delay);
                    i++;
                    break;

                case "--skip-profiles":
                    skipProfiles = true;
                    break;

                case "--refresh":
                    refresh = true;
                    break;

                case "--index-only":
                    indexOnly = true;
                    break;

                case "--pages-only":
                    pagesOnly = true;
                    break;

                case "--courses-only":
                    coursesOnly = true;
                    break;

                case "--allow-shrink":
                    allowShrink = true;
                    break;

                default:
                    Console.WriteLine($"Невідомий аргумент: {args[i]}");
                    break;
            }
        }

        return new ScraperOptions(outDir, newsPages, skipProfiles, refresh, indexOnly, pagesOnly, coursesOnly, allowShrink, delayMs);
    }
}

file static class CourseLinks
{
    public static readonly Regex Href = new(
        @"electronics\.lnu\.edu\.ua/course/(?<slug>[a-zA-Z0-9\-]+)|href=[\\""]*courses/(?<slug>[a-zA-Z0-9\-]+)",
        RegexOptions.Compiled);
}
