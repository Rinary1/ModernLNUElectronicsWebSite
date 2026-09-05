using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using WebSite.Data;

namespace WebSite.Scraper.Scraping;

public sealed class StaffScraper(IHtmlSource source)
{
    private const string SectionSelector = "article.staff section";
    private const string RowSelector = "table tbody tr";

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex ExternalMark =
        new(@"\s*\((?:сумісник|за сумісництвом)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HtmlParser _parser = new();

    public async Task<IReadOnlyList<StaffItem>> LoadPageAsync(string pageUrl, CancellationToken ct = default)
    {
        var html = await source.GetHtmlAsync(pageUrl, ct);
        return Parse(html, baseUrl: pageUrl);
    }

    public IReadOnlyList<StaffItem> Parse(string html, string? baseUrl = null)
    {
        var document = _parser.ParseDocument(html);
        var pageUri = baseUrl is null ? null : new Uri(baseUrl);

        var result = new List<StaffItem>();

        foreach (var section in document.QuerySelectorAll(SectionSelector))
        {
            var group = ClassifyGroup(section, pageUri);

            foreach (var row in section.QuerySelectorAll(RowSelector))
            {
                var item = ToStaffItem(row, group, pageUri);
                if (item is not null)
                    result.Add(item);
            }
        }

        return result;
    }

    private static StaffItem? ToStaffItem(IElement row, StaffGroup group, Uri? pageUri)
    {
        var nameLink = row.QuerySelector("td.name a");
        var fullName = Collapse(nameLink?.TextContent);
        var profileUrl = Absolutize(nameLink?.GetAttribute("href"), pageUri);

        // Ім'я + посилання на профіль обов'язкові; без них рядок не є працівником.
        if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(profileUrl))
            return null;

        var rawPosition = Collapse(row.QuerySelector("td.position")?.TextContent) ?? string.Empty;
        var isExternal = ExternalMark.IsMatch(rawPosition);
        var position = Collapse(ExternalMark.Replace(rawPosition, string.Empty)) ?? rawPosition;

        return new StaffItem
        {
            Group = group,
            FullName = fullName,
            Position = position,
            IsExternal = isExternal,
            Email = ExtractEmail(row.QuerySelector("td.email a")?.GetAttribute("href")),
            ProfileUrl = profileUrl,
        };
    }

    private static StaffGroup ClassifyGroup(IElement section, Uri? pageUri)
    {
        var heading = section.QuerySelector("h2");
        var url = Absolutize(heading?.QuerySelector("a")?.GetAttribute("href"), pageUri);
        var title = Collapse(heading?.TextContent) ?? "-";

        var kind =
            url?.Contains("/department/") == true ? DepartmentKindFromUrl(url) :
            url?.Contains("/laboratory/") == true ? StaffGroupKind.Laboratory :
            KindFromTitle(title);

        return new StaffGroup
        {
            Kind = kind,
            Title = title,
            Url = url,
        };
    }

    private static StaffGroupKind DepartmentKindFromUrl(string url) => url switch
    {
        _ when url.Contains("/department/optoelectronics") => StaffGroupKind.DepartmentOptoelectronics,
        _ when url.Contains("/department/radioelectronics") => StaffGroupKind.DepartmentRadioelectronics,
        _ when url.Contains("/department/radiophysics") => StaffGroupKind.DepartmentRadiophysics,
        _ when url.Contains("/department/sensor") => StaffGroupKind.DepartmentSensorElectronics,
        _ when url.Contains("/department/system-design") => StaffGroupKind.DepartmentSystemDesign,
        _ when url.Contains("/department/physic") => StaffGroupKind.DepartmentPhysicalBioelectronics,
        _ => StaffGroupKind.Other,
    };

    // Секції без посилання (деканат, секретаріат) або запасний варіант, якщо приберуть <a>.
    private static StaffGroupKind KindFromTitle(string title)
    {
        var t = title.ToLowerInvariant();

        if (t.Contains("секретаріат")) return StaffGroupKind.DeansOfficeSecretariat;
        if (t.Contains("деканат")) return StaffGroupKind.DeansOffice;
        if (t.Contains("лаборатор")) return StaffGroupKind.Laboratory;
        if (t.Contains("оптоелектрон")) return StaffGroupKind.DepartmentOptoelectronics;
        if (t.Contains("радіоелектрон")) return StaffGroupKind.DepartmentRadioelectronics;
        if (t.Contains("радіофізик")) return StaffGroupKind.DepartmentRadiophysics;
        if (t.Contains("сенсорн")) return StaffGroupKind.DepartmentSensorElectronics;
        if (t.Contains("системного проектування")) return StaffGroupKind.DepartmentSystemDesign;
        if (t.Contains("біомедичн")) return StaffGroupKind.DepartmentPhysicalBioelectronics;

        return StaffGroupKind.Other;
    }

    private static string? ExtractEmail(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return null;

        return href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            ? Uri.UnescapeDataString(href["mailto:".Length..]).Trim()
            : href.Trim();
    }

    private static string? Collapse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return WhitespaceRun.Replace(text, " ").Trim();
    }

    private static string? Absolutize(string? href, Uri? pageUri)
    {
        if (string.IsNullOrWhiteSpace(href))
            return null;

        if (pageUri is null || Uri.IsWellFormedUriString(href, UriKind.Absolute))
            return href;

        return Uri.TryCreate(pageUri, href, out var absolute) ? absolute.ToString() : href;
    }
}
