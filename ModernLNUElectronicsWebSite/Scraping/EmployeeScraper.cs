using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ModernLNUElectronicsWebSite.Scraping;

public sealed class EmployeeScraper(IHtmlSource source)
{
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex CssUrl = new(@"url\(\s*['""]?(?<u>[^'"")]+)", RegexOptions.Compiled);

    private readonly HtmlParser _parser = new();

    public async Task<EmployeeProfile> LoadAsync(string profileUrl, CancellationToken ct = default)
        => Parse(await source.GetHtmlAsync(profileUrl, ct), profileUrl);

    public EmployeeProfile Parse(string html, string profileUrl)
    {
        var document = _parser.ParseDocument(html);
        var pageUri = new Uri(profileUrl);
        var article = document.QuerySelector("article.employee")
                      ?? document.Body
                      ?? document.DocumentElement;

        var fields = ReadInfoFields(article);

        var positionValue = Field(fields, "Посада");
        var phoneValue = Field(fields, "Телефон");

        return new EmployeeProfile
        {
            ProfileUrl = profileUrl,
            FullName = Collapse(article.QuerySelector("h1.page-title")?.TextContent) ?? string.Empty,
            PhotoUrl = ReadPhotoUrl(article, pageUri),
            PositionText = Collapse(positionValue?.TextContent),
            DepartmentUrl = Absolutize(FindDepartmentHref(positionValue), pageUri),
            AcademicDegree = Collapse(Field(fields, "Науковий ступінь")?.TextContent),
            AcademicTitle = Collapse(Field(fields, "Вчене звання")?.TextContent),
            Phone = Collapse(phoneValue?.QuerySelector("a[href^='tel:']")?.TextContent ?? phoneValue?.TextContent),
            Email = ExtractEmail(Field(fields, "Електронна пошта")?.QuerySelector("a")?.GetAttribute("href")),
            Profiles = ReadProfiles(fields),
            ResearchInterests = Collapse(SectionBody(article, "Наукові інтереси")?.TextContent),
            Courses = ReadCourses(article, pageUri),
        };
    }

    private static Dictionary<string, IElement> ReadInfoFields(IElement article)
    {
        var map = new Dictionary<string, IElement>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in article.QuerySelectorAll("section.general .info p"))
        {
            var label = Collapse(p.QuerySelector(".label")?.TextContent)?.TrimEnd(':', ' ', ' ');
            var value = p.QuerySelector(".value");
            if (!string.IsNullOrEmpty(label) && value is not null)
                map[label!] = value;
        }

        return map;
    }

    private static IElement? Field(Dictionary<string, IElement> fields, string labelPrefix) =>
        fields.FirstOrDefault(kv => kv.Key.StartsWith(labelPrefix, StringComparison.OrdinalIgnoreCase)).Value;

    private static Dictionary<string, string> ReadProfiles(Dictionary<string, IElement> fields)
    {
        var wanted = new (string Needle, string Name)[]
        {
            ("Scholar", "Google Scholar"),
            ("ORCID", "ORCID"),
            ("Scopus", "Scopus"),
            ("Publons", "Web of Science"),
        };

        var result = new Dictionary<string, string>();

        foreach (var (needle, name) in wanted)
        {
            var value = fields.FirstOrDefault(kv => kv.Key.Contains(needle, StringComparison.OrdinalIgnoreCase)).Value;
            var href = value?.QuerySelector("a")?.GetAttribute("href");
            if (!string.IsNullOrWhiteSpace(href))
                result[name] = href!;
        }

        return result;
    }

    private static string? ReadPhotoUrl(IElement article, Uri pageUri)
    {
        var style = article.QuerySelector("section.general .photo")?.GetAttribute("style");
        if (string.IsNullOrEmpty(style))
            return null;

        var match = CssUrl.Match(style);
        return match.Success ? Absolutize(match.Groups["u"].Value.Trim(), pageUri) : null;
    }

    private static string? FindDepartmentHref(IElement? scope) =>
        scope?.QuerySelectorAll("a")
             .Select(a => a.GetAttribute("href"))
             .FirstOrDefault(h => h?.Contains("/department/") == true);

    private static IReadOnlyList<CourseLink> ReadCourses(IElement article, Uri pageUri)
    {
        var body = SectionBody(article, "Навчальні дисципліни");
        if (body is null)
            return Array.Empty<CourseLink>();

        return body.QuerySelectorAll("li a")
            .Select(a => new CourseLink(
                Collapse(a.TextContent) ?? string.Empty,
                Absolutize(a.GetAttribute("href"), pageUri) ?? string.Empty))
            .Where(c => c.Title.Length > 0)
            .ToList();
    }

    private static IElement? SectionBody(IElement article, string heading) =>
        article.QuerySelectorAll("section")
            .FirstOrDefault(s => string.Equals(
                Collapse(s.QuerySelector("h2")?.TextContent), heading, StringComparison.OrdinalIgnoreCase))
            ?.QuerySelector("div");

    private static string? ExtractEmail(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return null;

        return href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            ? Uri.UnescapeDataString(href["mailto:".Length..]).Trim()
            : href.Trim();
    }

    private static string? Collapse(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : WhitespaceRun.Replace(text, " ").Trim();

    private static string? Absolutize(string? href, Uri pageUri)
    {
        if (string.IsNullOrWhiteSpace(href))
            return null;

        if (Uri.IsWellFormedUriString(href, UriKind.Absolute))
            return href;

        return Uri.TryCreate(pageUri, href, out var absolute) ? absolute.ToString() : href;
    }
}
