using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ModernLNUElectronicsWebSite.Data;

namespace ModernLNUElectronicsWebSite.Scraper.Scraping;

public sealed class ArticleScraper(IHtmlSource source)
{
    private const string ArticleSelector = "main.content-area article";
    private const string TitleSelector = "h1.page-title";
    private const string MetaSelector = ".meta";

    private const string ThemeImageMarker = "/themes/";

    private static readonly string[] DateFormats =
    {
        "dd.MM.yyyy | HH:mm",
        "dd.MM.yyyy",
    };

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    private static readonly Regex HtmlTag = new(@"<[^>]+>", RegexOptions.Compiled);

    private readonly HtmlParser _parser = new();

    public async Task<MirrorPage> LoadAsync(string pageUrl, CancellationToken ct = default)
        => Parse(await source.GetHtmlAsync(pageUrl, ct), pageUrl);

    public MirrorPage Parse(string html, string pageUrl)
    {
        var document = _parser.ParseDocument(html);
        var pageUri = new Uri(pageUrl);

        var article = document.QuerySelector(ArticleSelector)
                      ?? document.QuerySelector("article")
                      ?? document.Body
                      ?? throw new InvalidOperationException($"Немає тіла сторінки: {pageUrl}");

        var title = Collapse(article.QuerySelector(TitleSelector)?.TextContent)
                    ?? Collapse(document.QuerySelector("title")?.TextContent)
                    ?? SiteUrls.Slug(pageUrl);

        var rawDate = Collapse(article.QuerySelector(MetaSelector)?.TextContent);

        article.QuerySelector(TitleSelector)?.Remove();
        article.QuerySelector(MetaSelector)?.Remove();

        var bodyHtml = ContentSanitizer.Sanitize(article, pageUri);

        return new MirrorPage
        {
            Slug = SiteUrls.Slug(pageUrl),
            SourceUrl = pageUrl,
            Title = title,
            RawDate = rawDate,
            PublishedAt = ParseDate(rawDate) ?? ParseMetaDate(document),
            CoverImageUrl = ReadCoverImage(document, bodyHtml),
            BodyHtml = bodyHtml,
            PlainText = PlainFrom(bodyHtml),
        };
    }

    private static string? ReadCoverImage(IDocument document, string bodyHtml)
    {
        var url = document.QuerySelectorAll("meta[property='og:image']")
            .Select(m => m.GetAttribute("content"))
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u)
                                 && !u.Contains(ThemeImageMarker, StringComparison.OrdinalIgnoreCase));

        if (url is null)
            return null;

        var stem = ImageStem(url);
        return stem.Length > 0 && bodyHtml.Contains(stem, StringComparison.OrdinalIgnoreCase) ? null : url;
    }

    private static string ImageStem(string url)
    {
        var name = Path.GetFileNameWithoutExtension(
            Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url);

        return name.EndsWith("-scaled", StringComparison.OrdinalIgnoreCase)
            ? name[..^"-scaled".Length]
            : name;
    }

    private static DateTime? ParseMetaDate(IDocument document)
    {
        var raw = document.QuerySelector("meta[property='article:published_time']")?.GetAttribute("content");

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static DateTime? ParseDate(string? raw) =>
        !string.IsNullOrWhiteSpace(raw)
        && DateTime.TryParseExact(raw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    private static string PlainFrom(string html) =>
        Collapse(System.Net.WebUtility.HtmlDecode(HtmlTag.Replace(html, " "))) ?? string.Empty;

    private static string? Collapse(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WhitespaceRun.Replace(value, " ").Trim();
}
