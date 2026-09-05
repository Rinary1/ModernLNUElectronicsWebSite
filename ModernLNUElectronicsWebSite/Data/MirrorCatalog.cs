namespace ModernLNUElectronicsWebSite.Data;

public sealed record MirrorPageRef(
    string Group,
    string Slug,
    string Title,
    string SourceUrl,
    bool Listed = true);

public static class MirrorCatalog
{
    public const string Applicants = "applicants";
    public const string Science = "science";

    public static IReadOnlyList<MirrorPageRef> Pages { get; } =
    [
        new(Applicants, "vstupna-kampaniia", "Вступна кампанія 2026/2027",
            $"{SiteUrls.Origin}/admission/vstupna-kampaniia-2021-2022-navchal-noho-roku/"),
        new(Applicants, "your-prospects", "Загальна інформація",
            $"{SiteUrls.Origin}/admission/your-prospects/"),
        new(Applicants, "osvitni-programy", "Освітні програми",
            $"{SiteUrls.Origin}/admission/osvitni-programy/"),
        new(Applicants, "admission", "Правила прийому",
            $"{SiteUrls.Origin}/admission/admission/"),
        new(Applicants, "institute-preparation", "Довузівська підготовка",
            $"{SiteUrls.Origin}/admission/institute-preparation/"),

        new(Applicants, "bachelor-software-engineering",
            "Інженерія програмного забезпечення (бакалавр)",
            $"{SiteUrls.Origin}/academics/bachelor/curriculum-software-engineering", Listed: false),
        new(Applicants, "bachelor-computer-science",
            "Комп'ютерні науки (бакалавр)",
            $"{SiteUrls.Origin}/academics/bachelor/curriculum-computer-technologies-2018", Listed: false),
        new(Applicants, "bachelor-it-systems",
            "Інформаційні системи та технології (бакалавр)",
            $"{SiteUrls.Origin}/academics/bachelor/curriculum-it-technologies", Listed: false),
        new(Applicants, "bachelor-electronics-computer-systems",
            "Електроніка та комп'ютерні системи (бакалавр)",
            $"{SiteUrls.Origin}/academics/bachelor/navchalnyy-plan-elektronika-ta-komp-iuterni-systemy",
            Listed: false),
        new(Applicants, "bachelor-sensor-systems",
            "Сенсорні та діагностичні електронні системи (бакалавр)",
            $"{SiteUrls.Origin}/academics/bachelor/curriculum-micro-and-nanotechnics", Listed: false),
        new(Applicants, "bachelor-hpc",
            "Високопродуктивний комп'ютинг (бакалавр)",
            $"{SiteUrls.Origin}/academics/bachelor/navchalnyy-plan-vysokoproduktyvnyy-komp-iutynh",
            Listed: false),
        new(Applicants, "master-computer-science",
            "Комп'ютерні науки (магістр)",
            $"{SiteUrls.Origin}/academics/master/curriculum-computer-technologies-2017", Listed: false),
        new(Applicants, "master-sensor-devices",
            "Прилади та матеріали сенсорної електроніки (магістр)",
            $"{SiteUrls.Origin}/academics/master/curriculum-micro-and-nanotechnics", Listed: false),
        new(Applicants, "zaochna-forma", "Заочна форма навчання",
            $"{SiteUrls.Origin}/academics/zaochna-forma/", Listed: false),

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

    public static IEnumerable<MirrorPageRef> ListedIn(string group) =>
        In(group).Where(p => p.Listed);

    public static MirrorPageRef? Find(string group, string? slug) =>
        slug is null ? null : In(group).FirstOrDefault(p => p.Slug == slug);

    public static string LabelOf(string group) => group switch
    {
        Applicants => "Абітурієнту",
        Science => "Наука",
        _ => group,
    };
}
