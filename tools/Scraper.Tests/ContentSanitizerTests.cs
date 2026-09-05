using ModernLNUElectronicsWebSite.Scraper.Scraping;
using Xunit;

namespace ModernLNUElectronicsWebSite.Scraper.Tests;

public class ContentSanitizerTests
{
    private static readonly Uri Page = new("https://electronics.lnu.edu.ua/news/pryklad/");

    private static string Clean(string html) => ContentSanitizer.SanitizeFragment(html, Page);

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<style>body{}</style>")]
    [InlineData("<iframe src=\"https://evil.test\"></iframe>")]
    [InlineData("<form><input name=\"x\" /></form>")]
    public void DropsUnsafeElements(string html) =>
        Assert.Equal(string.Empty, Clean(html));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    public void DropsUnsafeHrefs(string href)
    {
        var result = Clean($"<p><a href=\"{href}\">текст</a></p>");

        Assert.DoesNotContain(href, result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("текст", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://electronics.lnu.edu.ua/news/vitannia/", "news/vitannia")]
    [InlineData("https://electronics.lnu.edu.ua/employee/bojko-ya-v/", "staff/bojko-ya-v")]
    [InlineData("https://electronics.lnu.edu.ua/department/system-design/", "departments/system-design")]
    [InlineData("https://electronics.lnu.edu.ua/course/biofizyka/", "courses/biofizyka")]
    public void RewritesInternalLinksToMirrorRoutes(string href, string expected)
    {
        var result = Clean($"<p><a href=\"{href}\">текст</a></p>");

        Assert.Contains($"href=\"{expected}\"", result, StringComparison.Ordinal);
        Assert.DoesNotContain("target=", result, StringComparison.Ordinal);
    }

    [Fact]
    public void MarksForeignLinksAsExternal()
    {
        var result = Clean("<p><a href=\"https://lnu.edu.ua/\">університет</a></p>");

        Assert.Contains("target=\"_blank\"", result, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvesRelativeLinksAgainstThePage()
    {
        var result = Clean("<p><a href=\"/course/biofizyka/\">курс</a></p>");

        Assert.Contains("href=\"courses/biofizyka\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsDeclaredImageSize()
    {
        var result = Clean("<p><img src=\"https://electronics.lnu.edu.ua/x.png\" width=\"100\" height=\"100\" /></p>");

        Assert.Contains("width=\"100\"", result, StringComparison.Ordinal);
        Assert.Contains("height=\"100\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void PicksImageVariantForDeclaredWidth()
    {
        var result = Clean(
            "<p><img src=\"https://electronics.lnu.edu.ua/logo.png\" width=\"100\" height=\"100\" " +
            "srcset=\"https://electronics.lnu.edu.ua/logo-150x150.png 150w, " +
            "https://electronics.lnu.edu.ua/logo-300x300.png 300w, " +
            "https://electronics.lnu.edu.ua/logo.png 1200w\" /></p>");

        Assert.Contains("logo-150x150.png", result, StringComparison.Ordinal);
    }

    [Fact]
    public void PicksLargeImageVariantWhenSizeIsUnknown()
    {
        var result = Clean(
            "<p><img src=\"https://electronics.lnu.edu.ua/photo.jpg\" " +
            "srcset=\"https://electronics.lnu.edu.ua/photo-150x150.jpg 150w, " +
            "https://electronics.lnu.edu.ua/photo-1024.jpg 1024w\" /></p>");

        Assert.Contains("photo-1024.jpg", result, StringComparison.Ordinal);
    }

    [Fact]
    public void LazyLoadsOnlyImagesWithKnownSize()
    {
        var sized = Clean("<p><img src=\"https://electronics.lnu.edu.ua/a.png\" width=\"300\" height=\"200\" /></p>");
        var unsized = Clean("<p><img src=\"https://electronics.lnu.edu.ua/b.png\" /></p>");

        Assert.Contains("loading=\"lazy\"", sized, StringComparison.Ordinal);
        Assert.DoesNotContain("loading=", unsized, StringComparison.Ordinal);
    }

    [Fact]
    public void RemovesEmptyHeadingsButKeepsRealOnes()
    {
        var result = Clean("<h3></h3><h3>  </h3><h3>Правила</h3>");

        Assert.Equal("<h3>Правила</h3>", result);
    }

    [Fact]
    public void KeepsTables()
    {
        var result = Clean("<table><tr><th colspan=\"2\">Семестр</th></tr><tr><td>5</td><td>Залік</td></tr></table>");

        Assert.Contains("<table>", result, StringComparison.Ordinal);
        Assert.Contains("colspan=\"2\"", result, StringComparison.Ordinal);
        Assert.Contains("Залік", result, StringComparison.Ordinal);
    }

    [Fact]
    public void UnwrapsUnknownTagsKeepingText()
    {
        var result = Clean("<div><span>Текст</span> далі</div>");

        Assert.Equal("Текст далі", result);
    }
}
