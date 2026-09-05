using ModernLNUElectronicsWebSite.Data;
using Xunit;

namespace ModernLNUElectronicsWebSite.Scraper.Tests;

public class SiteUrlsTests
{
    [Theory]
    [InlineData("https://electronics.lnu.edu.ua/", "")]
    [InlineData("https://electronics.lnu.edu.ua/news/", "news")]
    [InlineData("https://electronics.lnu.edu.ua/news/vitannia/", "news/vitannia")]
    [InlineData("https://electronics.lnu.edu.ua/employee/bojko-ya-v/", "staff/bojko-ya-v")]
    [InlineData("https://electronics.lnu.edu.ua/department/system-design/", "departments/system-design")]
    [InlineData("https://electronics.lnu.edu.ua/course/biofizyka/", "courses/biofizyka")]
    public void MapsDetailPagesToMirrorRoutes(string url, string expected) =>
        Assert.Equal(expected, SiteUrls.ToMirrorRoute(url));

    [Theory]
    [InlineData("https://electronics.lnu.edu.ua/students/career/", "schedule")]
    [InlineData("https://electronics.lnu.edu.ua/students/rozklad-format-pdf/", "schedule")]
    [InlineData("https://electronics.lnu.edu.ua/students/government/", "students/government")]
    public void SpecificRoutesWinOverSectionGroups(string url, string expected) =>
        Assert.Equal(expected, SiteUrls.ToMirrorRoute(url));

    [Theory]
    [InlineData("https://electronics.lnu.edu.ua/about/staff/", "staff")]
    [InlineData("https://electronics.lnu.edu.ua/about/history-of-faculty/", "about/history-of-faculty")]
    [InlineData("https://electronics.lnu.edu.ua/academics/bachelor/", "academics/bachelor")]
    [InlineData("https://electronics.lnu.edu.ua/academics/bachelor/curriculum-software-engineering",
        "applicants/bachelor-software-engineering")]
    public void MapsCatalogPages(string url, string expected) =>
        Assert.Equal(expected, SiteUrls.ToMirrorRoute(url));

    [Theory]
    [InlineData("https://lnu.edu.ua/")]
    [InlineData("https://admission.lnu.edu.ua/guidelines-for-admission/")]
    [InlineData("https://electronics.lnu.edu.ua/wp-content/uploads/plan.pdf")]
    [InlineData("not a url")]
    public void LeavesForeignAndUnknownAddressesAlone(string url) =>
        Assert.Null(SiteUrls.ToMirrorRoute(url));

    [Fact]
    public void EveryCatalogPageHasItsOwnRoute()
    {
        var routes = MirrorCatalog.Pages
            .Select(p => SiteUrls.ToMirrorRoute(p.SourceUrl))
            .ToList();

        Assert.DoesNotContain(null, routes);
        Assert.Equal(routes.Count, routes.Distinct().Count());
    }

    [Fact]
    public void LongSlugsGetShortStableFileNames()
    {
        const string slug = "alhorytmy-ta-struktury-danykh-121-inzheneriia-prohramnoho-zabezpechennia-dodatkovyy-kurs";

        var name = SiteUrls.FileName(slug);

        Assert.True(name.Length <= 80, $"назва файлу задовга: {name.Length}");
        Assert.Equal(name, SiteUrls.FileName(slug));
        Assert.NotEqual(name, SiteUrls.FileName(slug + "-inshyy"));
    }

    [Fact]
    public void ShortSlugsAreKeptAsIs() =>
        Assert.Equal("biofizyka", SiteUrls.FileName("biofizyka"));
}
