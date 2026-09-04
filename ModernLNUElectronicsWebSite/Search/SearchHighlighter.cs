using System.Net;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace ModernLNUElectronicsWebSite.Search;

public static class SearchHighlighter
{
    public static string[] Tokenize(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? []
            : SearchService.Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public static MarkupString Highlight(string? text, string[] tokens)
    {
        if (string.IsNullOrEmpty(text))
            return default;

        if (tokens.Length == 0)
            return (MarkupString)WebUtility.HtmlEncode(text);

        var normalized = SearchService.Fold(text);
        var marks = new bool[text.Length];

        foreach (var token in tokens)
        {
            var from = 0;
            while (from <= normalized.Length - token.Length)
            {
                var index = normalized.IndexOf(token, from, StringComparison.Ordinal);
                if (index < 0)
                    break;

                for (var i = index; i < index + token.Length && i < marks.Length; i++)
                    marks[i] = true;

                from = index + token.Length;
            }
        }

        var builder = new StringBuilder(text.Length + 32);
        var open = false;

        for (var i = 0; i < text.Length; i++)
        {
            if (marks[i] && !open)
            {
                builder.Append("<mark>");
                open = true;
            }
            else if (!marks[i] && open)
            {
                builder.Append("</mark>");
                open = false;
            }

            builder.Append(WebUtility.HtmlEncode(text[i].ToString()));
        }

        if (open)
            builder.Append("</mark>");

        return (MarkupString)builder.ToString();
    }
}
