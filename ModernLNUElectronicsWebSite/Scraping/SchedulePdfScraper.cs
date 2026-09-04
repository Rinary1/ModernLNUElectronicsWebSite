using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ModernLNUElectronicsWebSite.Scraping;

public sealed class SchedulePdfScraper(IHtmlSource source)
{
    private const int ShortTitleThreshold = 16;

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    private readonly HtmlParser _parser = new();

    public async Task<IReadOnlyList<ScheduleDoc>> LoadPageAsync(string pageUrl, CancellationToken ct = default)
    {
        var html = await source.GetHtmlAsync(pageUrl, ct);
        return Parse(html, baseUrl: pageUrl);
    }

    public IReadOnlyList<ScheduleDoc> Parse(string html, string? baseUrl = null)
    {
        var document = _parser.ParseDocument(html);
        var pageUri = baseUrl is null ? null : new Uri(baseUrl);

        var article = document.QuerySelector("article") ?? document.Body;
        if (article is null)
            return Array.Empty<ScheduleDoc>();

        var seen = new HashSet<string>();
        var result = new List<ScheduleDoc>();

        foreach (var anchor in article.QuerySelectorAll("a[href]"))
        {
            var href = anchor.GetAttribute("href");
            if (href is null || !href.Contains(".pdf", StringComparison.OrdinalIgnoreCase))
                continue;

            var url = Absolutize(href, pageUri);
            if (url is null || !seen.Add(url))
                continue;

            var text = Collapse(anchor.TextContent) ?? "Документ";
            var section = NearestHeading(anchor);
            var title = text.Length <= ShortTitleThreshold && section is not null
                ? $"{section} — {text}"
                : text;

            result.Add(new ScheduleDoc(section ?? "Розклади", title, url));
        }

        return result;
    }

    private static string? NearestHeading(IElement anchor)
    {
        var block = anchor;
        while (block.ParentElement is { } parent
               && !string.Equals(parent.TagName, "ARTICLE", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(parent.TagName, "BODY", StringComparison.OrdinalIgnoreCase))
        {
            block = parent;
            if (parent.TagName is "P" or "LI" or "H2" or "H3" or "H4" or "DIV")
                break;
        }

        for (var sibling = block.PreviousElementSibling; sibling is not null; sibling = sibling.PreviousElementSibling)
        {
            if (sibling.TagName is "H1" or "H2" or "H3" or "H4" && sibling.QuerySelector("a") is null)
                return Collapse(sibling.TextContent);
        }

        return null;
    }

    private static string? Collapse(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WhitespaceRun.Replace(value, " ").Trim();

    private static string? Absolutize(string? href, Uri? pageUri)
    {
        if (string.IsNullOrWhiteSpace(href))
            return null;

        if (pageUri is null || Uri.IsWellFormedUriString(href, UriKind.Absolute))
            return href;

        return Uri.TryCreate(pageUri, href, out var absolute) ? absolute.ToString() : href;
    }
}
