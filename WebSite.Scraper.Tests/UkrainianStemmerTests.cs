using WebSite.Search;
using Xunit;

namespace WebSite.Scraper.Tests;

public class UkrainianStemmerTests
{

    [Theory]
    [InlineData("фізика", "фізичної")]
    [InlineData("дисципліна", "дисципліни")]
    [InlineData("розклад", "розкладу")]
    [InlineData("кафедра", "кафедри")]
    [InlineData("викладач", "викладачі")]
    [InlineData("новина", "новини")]
    public void MatchesWordForms(string one, string another) =>
        Assert.Equal(UkrainianStemmer.Stem(one), UkrainianStemmer.Stem(another));

    [Theory]
    [InlineData("студент", "студенти")]
    [InlineData("факультет", "факультети")]
    [InlineData("документ", "документи")]
    [InlineData("предмет", "предмети")]
    public void MatchesPluralsOfNounsEndingInT(string one, string another) =>
        Assert.Equal(UkrainianStemmer.Stem(one), UkrainianStemmer.Stem(another));

    [Theory]
    [InlineData("вивчати", "вивч")]
    [InlineData("робити", "роб")]
    public void StillStripsInfinitives(string infinitive, string expected) =>
        Assert.Equal(expected, UkrainianStemmer.Stem(infinitive));

    [Fact(Skip = "Відоме обмеження: «іспити» не відрізнити від інфінітива на -ити (робити, вчити)")]
    public void MatchesPluralsColidingWithInfinitives() =>
        Assert.Equal(UkrainianStemmer.Stem("іспит"), UkrainianStemmer.Stem("іспити"));

    [Theory]
    [InlineData("фізика", "математика")]
    [InlineData("кафедра", "кабінет")]
    public void KeepsDifferentWordsApart(string one, string another) =>
        Assert.NotEqual(UkrainianStemmer.Stem(one), UkrainianStemmer.Stem(another));

    [Theory]
    [InlineData("рік")]
    [InlineData("ІТ")]
    [InlineData("м")]
    public void LeavesShortWordsAlone(string word) =>
        Assert.Equal(word, UkrainianStemmer.Stem(word));

    [Fact]
    public void IsStable() =>
        Assert.Equal(UkrainianStemmer.Stem("дисципліни"), UkrainianStemmer.Stem(UkrainianStemmer.Stem("дисципліни")));

    [Theory]
    [InlineData("Вельгош", 'В')]
    [InlineData("  ярема", 'Я')]
    [InlineData("", null)]
    [InlineData("123", null)]
    public void ReadsFirstLetterForTheAlphabetFilter(string value, char? expected) =>
        Assert.Equal(expected, UkrainianAlphabet.FirstLetter(value));
}
