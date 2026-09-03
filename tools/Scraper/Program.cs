using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModernLNUElectronicsWebSite.Scraping;

var outDir = args.Length > 0 && !IsFlag(args[0]) ? args[0]
    : Path.Combine("ModernLNUElectronicsWebSite", "wwwroot", "data");

var newsPages = args.Skip(1).Select(a => int.TryParse(a, out var n) ? n : 0).FirstOrDefault(n => n > 0);
if (newsPages == 0) newsPages = 3;

var profilesArg = args.FirstOrDefault(a => a.StartsWith("profiles", StringComparison.OrdinalIgnoreCase));
var withProfiles = profilesArg is not null;
var profilesLimit = profilesArg?.Split(':') is [_, var raw] && int.TryParse(raw, out var lim) ? lim : int.MaxValue;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd(
    "ModernLNUElectronicsMirror/1.0 (+https://github.com/OWNER/REPO; scheduled scraper)");

var htmlSource = new HttpHtmlSource(http);
var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};

var newsScraper = new NewsScraper(htmlSource);
var news = new List<NewsItem>();
string? pageUrl = "https://electronics.lnu.edu.ua/news/";

for (var page = 1; page <= newsPages && pageUrl is not null; page++)
{
    Console.WriteLine($"news [{page}/{newsPages}] {pageUrl}");
    var result = await newsScraper.LoadPageAsync(pageUrl);
    news.AddRange(result.Items);
    pageUrl = result.NextPageUrl;
    if (pageUrl is not null)
        await Task.Delay(TimeSpan.FromSeconds(1.5));
}

var newsDeduped = news
    .GroupBy(i => i.Url)
    .Select(g => g.First())
    .OrderByDescending(i => i.PublishedAt ?? DateTime.MinValue)
    .ToList();

await WriteJson(Path.Combine(outDir, "news.json"), newsDeduped);
Console.WriteLine($"  -> {newsDeduped.Count} новин");

var staffScraper = new StaffScraper(htmlSource);
var staff = await staffScraper.LoadPageAsync("https://electronics.lnu.edu.ua/about/staff/");

await WriteJson(Path.Combine(outDir, "staff.json"), staff);
Console.WriteLine($"staff -> {staff.Count} осіб, груп: {staff.Select(s => s.Group.Title).Distinct().Count()}");

var administrationScraper = new AdministrationScraper(htmlSource);
var administration = await administrationScraper.LoadPageAsync("https://electronics.lnu.edu.ua/about/administration/");

await WriteJson(Path.Combine(outDir, "administration.json"), administration);
Console.WriteLine($"administration -> {administration.Count} " +
    $"(рада: {administration.Count(p => p.Section == AdministrationSection.Council)})");

if (withProfiles)
{
    var employeeScraper = new EmployeeScraper(htmlSource);
    var profileUrls = staff.Select(s => s.ProfileUrl).Distinct().Take(profilesLimit).ToList();
    var profiles = new List<EmployeeProfile>(profileUrls.Count);

    for (var i = 0; i < profileUrls.Count; i++)
    {
        var link = profileUrls[i];
        Console.WriteLine($"employee [{i + 1}/{profileUrls.Count}] {link}");
        try
        {
            profiles.Add(await employeeScraper.LoadAsync(link));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ! пропущено: {ex.Message}");
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    await WriteJson(Path.Combine(outDir, "employees.json"), profiles);
    Console.WriteLine($"employees -> {profiles.Count}/{profileUrls.Count}");
}

return;

async Task WriteJson<T>(string relativePath, T value)
{
    var full = Path.GetFullPath(relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    await File.WriteAllTextAsync(full, JsonSerializer.Serialize(value, jsonOptions) + Environment.NewLine);
}

static bool IsFlag(string arg) =>
    arg.StartsWith("profiles", StringComparison.OrdinalIgnoreCase) || int.TryParse(arg, out _);
