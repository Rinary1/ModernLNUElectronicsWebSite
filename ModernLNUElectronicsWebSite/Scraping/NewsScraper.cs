using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ModernLNUElectronicsWebSite.Scraping;

public sealed class NewsScraper(IHtmlSource source)
{
    private const string ArticleSelector = "main.content-area article";
    private const string TitleLinkSelector = "h2.title a";
    private const string MetaSelector = ".meta";
    private const string ExcerptSelector = ".excerpt > p";
    private const string NextPageSelector = "a.link.next";

    private static readonly string[] DateFormats =
    {
        "dd.MM.yyyy | HH:mm",
        "dd.MM.yyyy",
    };

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    private readonly HtmlParser _parser = new();

    public async Task<NewsPage> LoadPageAsync(string pageUrl, CancellationToken ct = default)
    {
        var html = await source.GetHtmlAsync(pageUrl, ct);
        return Parse(html, baseUrl: pageUrl);
    }

    public NewsPage Parse(string html, string? baseUrl = null)
    {
        var document = _parser.ParseDocument(html);
        var pageUri = baseUrl is null ? null : new Uri(baseUrl);

        var items = document
            .QuerySelectorAll(ArticleSelector)
            .Select(article => ToNewsItem(article, pageUri))
            .OfType<NewsItem>()
            .ToList();

        var nextHref = document.QuerySelector(NextPageSelector)?.GetAttribute("href");

        return new NewsPage
        {
            Items = items,
            NextPageUrl = Absolutize(nextHref, pageUri),
        };
    }

    private static NewsItem? ToNewsItem(IElement article, Uri? pageUri)
    {
        var link = article.QuerySelector(TitleLinkSelector);
        var title = Collapse(link?.TextContent);
        var url = Absolutize(link?.GetAttribute("href"), pageUri);

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(url))
            return null;

        var rawDate = Collapse(article.QuerySelector(MetaSelector)?.TextContent);
        var excerpt = Collapse(article.QuerySelector(ExcerptSelector)?.TextContent);

        return new NewsItem
        {
            Title = title,
            Slug = SiteUrls.Slug(url),
            Url = url,
            Excerpt = string.IsNullOrEmpty(excerpt) ? null : excerpt,
            PublishedAt = TryParseDate(rawDate),
            RawDate = rawDate,
        };
    }

    private static DateTime? TryParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return DateTime.TryParseExact(
            raw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
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
