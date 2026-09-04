using System.Text.RegularExpressions;
using ModernLNUElectronicsWebSite.Data;

namespace ModernLNUElectronicsWebSite.Scraper;

public static partial class SearchIndexBuilder
{
    private const int MaxTextLength = 8000;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTag();

    public static string Plain(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = HtmlTag().Replace(html, " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = WhitespaceRun().Replace(text, " ").Trim();

        return text.Length <= MaxTextLength ? text : text[..MaxTextLength];
    }

    public static string Join(params string?[] parts) =>
        WhitespaceRun().Replace(string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p))), " ").Trim();

    public static IEnumerable<SearchDoc> StaticPages()
    {
        var pages = new (string Route, string Title, string Text, string? Source)[]
        {
            ("", "Головна", "головна новини факультет електроніки", $"{SiteUrls.Origin}/"),
            ("about", "Про факультет", "про факультет партнери історія",
                $"{SiteUrls.Origin}/about/introduction/"),
            ("departments", "Кафедри", "кафедри підрозділи лабораторії склад",
                $"{SiteUrls.Origin}/about/departments/"),
            ("staff", "Співробітники", "співробітники викладачі персонал",
                $"{SiteUrls.Origin}/about/staff/"),
            ("administration", "Адміністрація", "деканат рада факультету адміністрація",
                $"{SiteUrls.Origin}/about/administration/"),
            ("news", "Новини та події", "новини події анонси", $"{SiteUrls.Origin}/news/"),
            ("schedule", "Розклад", "розклад занять іспити заліки пари сесія",
                $"{SiteUrls.Origin}/students/career/"),
            ("applicants", "Абітурієнту", "вступ абітурієнт спеціальності освітні програми",
                $"{SiteUrls.Origin}/admission/your-prospects/"),
            ("science", "Наука", "наука дослідження конференції публікації",
                $"{SiteUrls.Origin}/research/research-areas/"),
            ("contacts", "Контакти", "контакти адреса телефон пошта як дістатися", null),
        };

        return pages.Select(p => new SearchDoc(
            Id: $"page:{p.Route}",
            Kind: SearchKind.Page,
            Title: p.Title,
            Route: p.Route,
            SourceUrl: p.Source,
            Subtitle: "Розділ дзеркала",
            Text: p.Text,
            Date: null));
    }
}
