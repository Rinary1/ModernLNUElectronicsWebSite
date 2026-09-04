using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ModernLNUElectronicsWebSite.Data;

namespace ModernLNUElectronicsWebSite.Scraper.Scraping;

public sealed class AdministrationScraper(IHtmlSource source)
{
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> RankSingular = new(StringComparer.OrdinalIgnoreCase)
    {
        ["професори"] = "професор",
        ["доценти"] = "доцент",
        ["студенти"] = "студент",
        ["асистенти"] = "асистент",
    };

    private readonly HtmlParser _parser = new();

    public async Task<IReadOnlyList<AdministrationPerson>> LoadPageAsync(string pageUrl, CancellationToken ct = default)
    {
        var html = await source.GetHtmlAsync(pageUrl, ct);
        return Parse(html, baseUrl: pageUrl);
    }

    public IReadOnlyList<AdministrationPerson> Parse(string html, string? baseUrl = null)
    {
        var document = _parser.ParseDocument(html);
        var pageUri = baseUrl is null ? null : new Uri(baseUrl);
        var people = new List<AdministrationPerson>();

        ReadSection(document.QuerySelector("article.administration section.dean-office"),
            AdministrationSection.DeansOffice, people, pageUri);
        ReadSection(document.QuerySelector("article.administration section.council"),
            AdministrationSection.Council, people, pageUri);

        return people;
    }

    private static void ReadSection(IElement? section, AdministrationSection kind,
        List<AdministrationPerson> output, Uri? pageUri)
    {
        if (section is null)
            return;

        foreach (var row in section.QuerySelectorAll("tr"))
        {
            var nameCell = row.QuerySelector("td.name");
            if (nameCell is null)
                continue; // рядок-заголовок ("Заступники декана", colspan=3)

            var (role, roleDetail) = ResolveRole(row.ClassName ?? string.Empty,
                Collapse(row.QuerySelector("td.position")?.TextContent), kind);

            var rank = NormalizeRank(Collapse(row.QuerySelector("td.rank")?.TextContent));

            var links = nameCell.QuerySelectorAll("a").ToList();

            if (links.Count > 0)
            {
                foreach (var link in links)
                {
                    var name = Collapse(link.TextContent);
                    if (string.IsNullOrEmpty(name))
                        continue;

                    output.Add(new AdministrationPerson
                    {
                        Section = kind,
                        Role = role,
                        RoleDetail = roleDetail,
                        Rank = rank,
                        Name = name,
                        ProfileUrl = Absolutize(link.GetAttribute("href"), pageUri),
                    });
                }
            }
            else
            {
                foreach (var name in SplitPlainNames(nameCell.TextContent))
                {
                    output.Add(new AdministrationPerson
                    {
                        Section = kind,
                        Role = role,
                        RoleDetail = roleDetail,
                        Rank = rank ?? "студент",
                        Name = name,
                        ProfileUrl = null,
                    });
                }
            }
        }
    }

    private static (string Role, string? Detail) ResolveRole(string rowClass, string? positionText, AdministrationSection kind)
    {
        // Порядок важливий: "dean-deputy" перевіряємо раніше за "dean".
        if (rowClass.Contains("dean-deputy"))
            return ("Заступник декана", NullIfEmpty(positionText));
        if (rowClass.Contains("dean"))
            return (positionText is { Length: > 0 } ? positionText : "Декан", null);
        if (rowClass.Contains("head-deputy"))
            return ("Заступник голови ради", null);
        if (rowClass.Contains("head"))
            return (positionText is { Length: > 0 } ? positionText : "Голова ради", null);
        if (rowClass.Contains("secretary"))
            return (positionText is { Length: > 0 } ? positionText : "Секретар ради", null);
        if (rowClass.Contains("members"))
            return ("Член ради", null);

        var fallback = positionText is { Length: > 0 }
            ? positionText
            : kind == AdministrationSection.Council ? "Член ради" : "Деканат";
        return (fallback, null);
    }

    private static string? NormalizeRank(string? rank)
    {
        var value = rank?.TrimEnd(':', ' ', ' ');
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return RankSingular.TryGetValue(value, out var singular) ? singular : value;
    }

    private static IEnumerable<string> SplitPlainNames(string? text) =>
        (text ?? string.Empty)
            .Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Collapse(s))
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!);

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    private static string? Collapse(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : WhitespaceRun.Replace(text, " ").Trim();

    private static string? Absolutize(string? href, Uri? pageUri)
    {
        if (string.IsNullOrWhiteSpace(href))
            return null;

        if (pageUri is null || Uri.IsWellFormedUriString(href, UriKind.Absolute))
            return href;

        return Uri.TryCreate(pageUri, href, out var absolute) ? absolute.ToString() : href;
    }
}
