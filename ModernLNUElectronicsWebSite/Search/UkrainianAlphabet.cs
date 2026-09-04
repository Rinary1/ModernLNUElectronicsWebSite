namespace ModernLNUElectronicsWebSite.Search;

public static class UkrainianAlphabet
{
    public const string Letters = "АБВГҐДЕЄЖЗИІЇЙКЛМНОПРСТУФХЦЧШЩЬЮЯ";

    public static char? FirstLetter(string? value)
    {
        var trimmed = value?.TrimStart();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        var upper = char.ToUpperInvariant(trimmed[0]);
        return Letters.Contains(upper) ? upper : null;
    }

    public static int Compare(string? left, string? right)
    {
        left ??= string.Empty;
        right ??= string.Empty;

        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var order = Rank(left[i]).CompareTo(Rank(right[i]));
            if (order != 0)
                return order;
        }

        return left.Length.CompareTo(right.Length);
    }

    public static IComparer<string> Comparer { get; } =
        Comparer<string>.Create((a, b) => Compare(a, b));

    private static int Rank(char c)
    {
        var index = Letters.IndexOf(char.ToUpperInvariant(c));
        return index >= 0 ? index : Letters.Length + c;
    }
}
