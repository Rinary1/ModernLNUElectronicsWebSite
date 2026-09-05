namespace WebSite.Content;

public static class MetaText
{
    private const int MaxLength = 160;

    public static string? Summarize(string? text, int max = MaxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length <= max)
            return collapsed;

        var cut = collapsed.LastIndexOf(' ', max - 1);
        return collapsed[..(cut > max / 2 ? cut : max - 1)].TrimEnd(' ', ',', '.', ';', ':', '-') + "...";
    }

    public static string? Join(params string?[] parts)
    {
        var value = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return value.Length == 0 ? null : value;
    }
}
