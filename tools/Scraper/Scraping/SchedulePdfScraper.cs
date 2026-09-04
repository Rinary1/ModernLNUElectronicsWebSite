using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ModernLNUElectronicsWebSite.Data;

namespace ModernLNUElectronicsWebSite.Scraper.Scraping;

public sealed class SchedulePdfScraper(IHtmlSource source)
{
    private const int ShortTitleThreshold = 24;

    private const int StrayAnchorLength = 6;

    private const int MaxTitleLength = 140;

    private const int PseudoHeadingLevel = 5;

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    private readonly HtmlParser _parser = new();

    public async Task<IReadOnlyList<ScheduleDoc>> LoadPageAsync(
        string pageUrl, ScheduleCategory category, CancellationToken ct = default)
        => Parse(await source.GetHtmlAsync(pageUrl, ct), category, pageUrl);

    public IReadOnlyList<ScheduleDoc> Parse(string html, ScheduleCategory category, string pageUrl)
    {
        var document = _parser.ParseDocument(html);
        var pageUri = new Uri(pageUrl);

        var article = document.QuerySelector("main.content-area article") ?? document.Body;
        if (article is null)
            return Array.Empty<ScheduleDoc>();

        var headings = new string?[PseudoHeadingLevel + 1];
        var fallbackSection = Collapse(article.QuerySelector("h1.page-title")?.TextContent) ?? "Документи";

        var seen = new HashSet<string>();
        var docs = new List<ScheduleDoc>();

        foreach (var element in article.QuerySelectorAll("*"))
        {
            var level = HeadingLevel(element);
            if (level > 0)
            {
                headings[level] = Collapse(element.TextContent);
                for (var deeper = level + 1; deeper < headings.Length; deeper++)
                    headings[deeper] = null;
            }

            if (!IsPdfLink(element))
                continue;

            if (!Effective(PdfLinks(BlockOf(element))).Contains(element))
                continue;

            var url = Absolutize(element.GetAttribute("href"), pageUri);
            if (url is null || !seen.Add(url))
                continue;

            var ownLevel = AncestorHeadingLevel(element, article);
            var section = NearestSection(headings, ownLevel) ?? fallbackSection;

            docs.Add(new ScheduleDoc(category, section, TitleFor(element), url, pageUrl));
        }

        return Disambiguate(docs);
    }

    private static int HeadingLevel(IElement element) => element.TagName.ToUpperInvariant() switch
    {
        "H1" => 1,
        "H2" => 2,
        "H3" => 3,
        "H4" => 4,
        "P" when IsPseudoHeading(element) => PseudoHeadingLevel,
        _ => 0,
    };

    private static bool IsPseudoHeading(IElement paragraph) =>
        paragraph.QuerySelector("a") is null
        && paragraph.QuerySelector("strong, b") is not null
        && !string.IsNullOrWhiteSpace(paragraph.TextContent)
        && Collapse(paragraph.TextContent)!.Length
           == Collapse(paragraph.QuerySelectorAll("strong, b").LastOrDefault()?.TextContent)?.Length;

    private static int AncestorHeadingLevel(IElement anchor, IElement article)
    {
        for (var node = anchor.ParentElement; node is not null && node != article; node = node.ParentElement)
        {
            var level = HeadingLevel(node);
            if (level > 0)
                return level;
        }

        return 0;
    }

    private static string? NearestSection(string?[] headings, int ownLevel)
    {
        var from = ownLevel > 0 ? ownLevel - 1 : headings.Length - 1;

        for (var level = from; level >= 1; level--)
        {
            if (headings[level] is { Length: > 0 } heading)
                return heading;
        }

        return null;
    }

    private static string TitleFor(IElement anchor)
    {
        var block = BlockOf(anchor);
        var links = PdfLinks(block);
        var effective = Effective(links);
        var text = Collapse(anchor.TextContent);

        var useBlock = effective.Count == 1
                       && (text is null || text.Length <= ShortTitleThreshold || effective.Count < links.Count);

        var title = useBlock ? Collapse(block.TextContent) ?? text : text;

        return Truncate(title ?? "Документ", MaxTitleLength);
    }

    private static List<IElement> Effective(List<IElement> links)
    {
        if (links.Count < 2)
            return links;

        var substantive = links
            .Where(a => Collapse(a.TextContent) is { Length: >= StrayAnchorLength })
            .ToList();

        return substantive.Count > 0 ? substantive : links;
    }

    private static List<IElement> PdfLinks(IElement block) =>
        block.QuerySelectorAll("a[href]").Where(IsPdfLink).ToList();

    private static IElement BlockOf(IElement anchor)
    {
        for (var node = anchor.ParentElement; node is not null; node = node.ParentElement)
        {
            if (node.TagName is "LI" or "P" or "TD" or "H1" or "H2" or "H3" or "H4")
                return node;
        }

        return anchor;
    }

    private static List<ScheduleDoc> Disambiguate(List<ScheduleDoc> docs) => docs
        .GroupBy(d => (d.Section, d.Title))
        .SelectMany(group => group.Count() == 1
            ? group
            : group.Select(d => d with { Title = $"{d.Title} ({FileNameOf(d.Url)})" }))
        .ToList();

    private static string FileNameOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? Path.GetFileName(uri.AbsolutePath) : url;

    private static bool IsPdfLink(IElement element) =>
        element.TagName.Equals("A", StringComparison.OrdinalIgnoreCase)
        && element.GetAttribute("href") is { } href
        && href.Contains(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "...";

    private static string? Collapse(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WhitespaceRun.Replace(value, " ").Trim();

    private static string? Absolutize(string? href, Uri pageUri)
    {
        if (string.IsNullOrWhiteSpace(href))
            return null;

        if (Uri.IsWellFormedUriString(href, UriKind.Absolute))
            return href;

        return Uri.TryCreate(pageUri, href, out var absolute) ? absolute.ToString() : href;
    }
}
