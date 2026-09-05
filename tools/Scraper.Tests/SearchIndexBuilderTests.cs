using Xunit;

namespace ModernLNUElectronicsWebSite.Scraper.Tests;

public class SearchIndexBuilderTests
{
    [Fact]
    public void SeparatesAdjacentCells()
    {
        var text = SearchIndexBuilder.Plain("<tr><td>2</td><td>32</td><td>доцент Вельгош С. Р.</td></tr>");

        Assert.Equal("2 32 доцент Вельгош С. Р.", text);
    }

    [Fact]
    public void DecodesEntitiesAndCollapsesWhitespace()
    {
        var text = SearchIndexBuilder.Plain("<p>Комп&#8217;ютерні   науки</p>\n<p>&nbsp;та&nbsp;технології</p>");

        Assert.Equal("Комп’ютерні науки та технології", text);
    }

    [Fact]
    public void TruncatesToRequestedLength()
    {
        var text = SearchIndexBuilder.Plain(new string('а', 5000), maxLength: 100);

        Assert.Equal(100, text.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReturnsEmptyForBlankInput(string? html) =>
        Assert.Equal(string.Empty, SearchIndexBuilder.Plain(html));

    [Fact]
    public void JoinSkipsEmptyParts() =>
        Assert.Equal("Вельгош доцент", SearchIndexBuilder.Join("Вельгош", null, "  ", "доцент"));
}
