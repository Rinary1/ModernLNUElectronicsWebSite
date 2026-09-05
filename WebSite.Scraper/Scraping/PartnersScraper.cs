using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using WebSite.Data;

namespace WebSite.Scraper.Scraping;

public sealed class PartnersScraper(IHtmlSource source)
{
    private const int MinDescriptionLength = 60;
    private const int MaxNameOffset = 40;

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);
    private static readonly char[] NameSeparators = { '–', '—', '−', '-' };

    private readonly HtmlParser _parser = new();

    public async Task<IReadOnlyList<Partner>> LoadPageAsync(string pageUrl, CancellationToken ct = default)
    {
        var html = await source.GetHtmlAsync(pageUrl, ct);
        return Parse(html, baseUrl: pageUrl);
    }

    public IReadOnlyList<Partner> Parse(string html, string? baseUrl = null)
    {
        var document = _parser.ParseDocument(html);
        var pageUri = baseUrl is null ? null : new Uri(baseUrl);

        var article = document.QuerySelector("article.content") ?? document.Body;
        if (article is null)
            return Array.Empty<Partner>();

        var partners = new List<Partner>();
        string? pendingLogo = null;

        foreach (var paragraph in article.QuerySelectorAll("p"))
        {
            var alignLeftLogo = paragraph
                .QuerySelectorAll("img")
                .FirstOrDefault(img => (img.GetAttribute("class") ?? string.Empty).Contains("alignleft"));
            if (alignLeftLogo is not null)
                pendingLogo = Absolutize(alignLeftLogo.GetAttribute("src"), pageUri);

            var text = Collapse(paragraph.TextContent);
            if (text is null || text.Length < MinDescriptionLength)
                continue;

            var links = paragraph.QuerySelectorAll("a").ToList();
            var (name, url) = ResolveNameAndUrl(text, links);
            if (string.IsNullOrEmpty(name))
                continue;

            partners.Add(new Partner
            {
                Name = name,
                Url = url,
                LogoUrl = pendingLogo,
                Description = text,
            });

            pendingLogo = null;
        }

        return partners;
    }

    private static (string Name, string? Url) ResolveNameAndUrl(string text, IReadOnlyList<IElement> links)
    {
        foreach (var link in links)
        {
            var linkText = Collapse(link.TextContent);
            if (string.IsNullOrEmpty(linkText) || linkText.Length > 60)
                continue;

            var offset = text.IndexOf(linkText, StringComparison.Ordinal);
            if (offset >= 0 && offset <= MaxNameOffset)
                return (linkText, link.GetAttribute("href"));
        }

        var cut = text.IndexOfAny(NameSeparators);
        var fallbackName = (cut > 0 ? text[..cut] : text).Trim();
        if (fallbackName.Length is 0 or > 60)
            return (string.Empty, null);

        var lastHref = links.Count > 0 ? links[^1].GetAttribute("href") : null;
        return (fallbackName, lastHref);
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
