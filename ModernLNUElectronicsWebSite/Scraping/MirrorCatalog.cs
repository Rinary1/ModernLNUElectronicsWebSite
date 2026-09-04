namespace ModernLNUElectronicsWebSite.Scraping;

public sealed record MirrorPageRef(string Group, string Slug, string Title, string SourceUrl);

public static class MirrorCatalog
{
    public const string Applicants = "applicants";
    public const string Science = "science";

    public static IReadOnlyList<MirrorPageRef> Pages { get; } =
    [
        new(Applicants, "your-prospects", "Загальна інформація",
            $"{SiteUrls.Origin}/admission/your-prospects/"),
        new(Applicants, "osvitni-programy", "Освітні програми",
            $"{SiteUrls.Origin}/admission/osvitni-programy/"),
        new(Applicants, "admission", "Правила прийому",
            $"{SiteUrls.Origin}/admission/admission/"),
        new(Applicants, "institute-preparation", "Довузівська підготовка",
            $"{SiteUrls.Origin}/admission/institute-preparation/"),

        new(Science, "research-areas", "Напрями досліджень",
            $"{SiteUrls.Origin}/research/research-areas/"),
        new(Science, "national-programs", "Національні програми",
            $"{SiteUrls.Origin}/research/national-programs/"),
        new(Science, "mizhnarodni-naukovi-proiekty", "Міжнародні наукові проєкти",
            $"{SiteUrls.Origin}/research/mizhnarodni-naukovi-proiekty/"),
        new(Science, "awards", "Відзнаки і нагороди",
            $"{SiteUrls.Origin}/research/awards/"),
        new(Science, "seminary", "Науковий семінар",
            $"{SiteUrls.Origin}/research/seminary/"),
        new(Science, "conferences", "Конференції",
            $"{SiteUrls.Origin}/research/conferences/"),
        new(Science, "publications", "Видання",
            $"{SiteUrls.Origin}/research/publications/"),
    ];

    public static IEnumerable<MirrorPageRef> In(string group) =>
        Pages.Where(p => p.Group == group);
}
