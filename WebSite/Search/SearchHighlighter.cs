using System.Net;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace WebSite.Search;

public static class SearchHighlighter
{
    private const string Apostrophes = "'ʼ’‘";

    public static string[] Tokenize(string? query) => SearchService.Stems(query);

    public static MarkupString Highlight(string? text, string[] stems)
    {
        if (string.IsNullOrEmpty(text))
            return default;

        if (stems.Length == 0)
            return (MarkupString)WebUtility.HtmlEncode(text);

        var builder = new StringBuilder(text.Length + 32);
        var start = 0;

        while (start < text.Length)
        {
            var end = start;
            var isWord = IsWordChar(text[start]);

            while (end < text.Length && IsWordChar(text[end]) == isWord)
                end++;

            var run = text[start..end];

            if (isWord && stems.Contains(UkrainianStemmer.Stem(SearchService.Fold(run))))
            {
                builder.Append("<mark>").Append(WebUtility.HtmlEncode(run)).Append("</mark>");
            }
            else
            {
                builder.Append(WebUtility.HtmlEncode(run));
            }

            start = end;
        }

        return (MarkupString)builder.ToString();
    }
    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || Apostrophes.Contains(c);
}
