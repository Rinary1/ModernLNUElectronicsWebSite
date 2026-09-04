using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModernLNUElectronicsWebSite.Scraping;

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

await ScrapeNewsAsync();
var staff = await ScrapeStaffAsync();
await ScrapeAdministrationAsync();
await ScrapePartnersAsync();
await ScrapeScheduleAsync();
await ScrapeMirroredPagesAsync();
await ScrapeDepartmentsAsync(staff);
await ScrapeEmployeesAsync(staff);

Console.WriteLine("Готово.");
return;

async Task ScrapeNewsAsync()
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

    var index = collected
        .GroupBy(i => i.Url)
        .Select(g => g.First())
        .OrderByDescending(i => i.PublishedAt ?? DateTime.MinValue)
        .ToList();

    await store.WriteAsync("news.json", index);
    Console.WriteLine($"  -> {index.Count} новин у стрічці");

    await MirrorEachAsync("news", index.Select(i => (i.Slug, i.Url)));
}

async Task<List<StaffItem>> ScrapeStaffAsync()
{
    var staffList = await new StaffScraper(htmlSource).LoadPageAsync($"{SiteUrls.Origin}/about/staff/");
    await store.WriteAsync("staff.json", staffList);
    Console.WriteLine($"staff -> {staffList.Count} осіб, підрозділів: " +
        $"{staffList.Select(s => s.Group.Title).Distinct().Count()}");

    return staffList.ToList();
}

async Task ScrapeAdministrationAsync()
{
    var people = await new AdministrationScraper(htmlSource).LoadPageAsync($"{SiteUrls.Origin}/about/administration/");
    await store.WriteAsync("administration.json", people);
    Console.WriteLine($"administration -> {people.Count} " +
        $"(рада: {people.Count(p => p.Section == AdministrationSection.Council)})");
}

async Task ScrapePartnersAsync()
{
    var partners = await new PartnersScraper(htmlSource).LoadPageAsync($"{SiteUrls.Origin}/about/introduction/");
    await store.WriteAsync("partners.json", partners);
    Console.WriteLine($"partners -> {partners.Count}");
}

async Task ScrapeScheduleAsync()
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

    await store.WriteAsync("schedule.json", docs);
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

async Task ScrapeDepartmentsAsync(List<StaffItem> staffList)
{
    var urls = staffList
        .Select(s => s.Group.Url)
        .OfType<string>()
        .Distinct()
        .ToList();

    await MirrorEachAsync("departments", urls.Select(u => (SiteUrls.Slug(u), u)));
    Console.WriteLine($"departments -> {urls.Count} сторінок підрозділів");
}

async Task ScrapeEmployeesAsync(List<StaffItem> staffList)
{
    if (options.SkipProfiles)
    {
        Console.WriteLine("employees -> пропущено (--skip-profiles)");
        return;
    }

    var scraper = new EmployeeScraper(htmlSource);
    var urls = staffList.Select(s => s.ProfileUrl).Distinct().ToList();
    var written = 0;

    foreach (var url in urls)
    {
        var path = $"employees/{SiteUrls.Slug(url)}.json";
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
}

async Task MirrorEachAsync(string folder, IEnumerable<(string Slug, string Url)> items)
{
    var written = 0;

    foreach (var (slug, url) in items)
    {
        if (slug.Length == 0)
            continue;

        var path = $"{folder}/{slug}.json";
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
    };

    public bool Exists(string relativePath) => File.Exists(Path.Combine(root, relativePath));

    public async Task WriteAsync<T>(string relativePath, T value)
    {
        var full = Path.GetFullPath(Path.Combine(root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, JsonSerializer.Serialize(value, Options) + Environment.NewLine);
    }
}

file sealed record ScraperOptions(string OutDir, int NewsPages, bool SkipProfiles, bool Refresh, int DelayMs)
{
    public static ScraperOptions Parse(string[] args)
    {
        var outDir = Path.Combine("ModernLNUElectronicsWebSite", "wwwroot", "data");
        var newsPages = 3;
        var skipProfiles = false;
        var refresh = false;
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

                default:
                    Console.WriteLine($"Невідомий аргумент: {args[i]}");
                    break;
            }
        }

        return new ScraperOptions(outDir, newsPages, skipProfiles, refresh, delayMs);
    }
}
