namespace WebSite.Search;

public static class UkrainianStemmer
{
    private const string Vowels = "аеиоуяюєїіыэё";

    private const int MinLength = 4;

    private const int MinStem = 3;

    private static readonly string[] Gerunds =
        ["івшись", "ившись", "увшись", "івши", "ивши", "увши", "ючись", "ачись", "вши", "ючи", "учи", "ачи", "ячи"];

    private static readonly string[] Reflexive = ["ся", "сь"];

    private static readonly string[] Adjectival =
        ["ього", "ьому", "ими", "їми", "ого", "ому", "ої", "ій", "их", "їх", "ий", "им", "ім", "ою", "ею", "ая", "яя"];

    private static readonly string[] Verbal =
        ["увала", "ували", "увало", "увати", "ував", "ються", "ити", "іти", "ати", "яти",
         "ють", "ать", "ять", "уть", "ила", "ило", "или", "ена", "ено", "ені", "ете", "йте",
         "ла", "ло", "ли", "ть"];

    private static readonly string[] Nouns =
        ["іями", "иями", "ами", "ями", "ові", "еві", "ієї", "ією", "іях", "ах", "ях", "ов", "ев", "ів", "ей", "ой",
         "ям", "ом", "ем", "ам", "ію", "ью", "ья", "ия", "ье", "ы", "ь", "ю", "я", "у", "о", "е", "и", "і", "ї", "а"];

    private static readonly string[] Derivational =
        ["ність", "ість", "ощ", "ств", "тв", "ичн", "ічн", "ськ", "цьк", "зьк",
         "ов", "ев", "ик", "ік", "иц", "ац", "ец", "ач", "ок"];

    public static string Stem(string word)
    {
        if (word.Length < MinLength)
            return word;

        var rv = RvStart(word);
        if (rv >= word.Length)
            return word;

        var stem = word;

        if (!TryCut(ref stem, rv, Gerunds))
        {
            TryCut(ref stem, rv, Reflexive);

            if (!TryCut(ref stem, rv, Adjectival) && !TryCut(ref stem, rv, Verbal))
                TryCut(ref stem, rv, Nouns);
        }

        TryCut(ref stem, rv, ["и", "і", "ї", "ь"]);

        if (stem.Length > MinStem && stem[^1] == stem[^2])
            stem = stem[..^1];

        TryCut(ref stem, rv, Derivational);

        return stem;
    }

    private static int RvStart(string word)
    {
        for (var i = 0; i < word.Length; i++)
        {
            if (Vowels.Contains(word[i]))
                return i + 1;
        }

        return word.Length;
    }

    private static bool TryCut(ref string stem, int rv, string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            var cut = stem.Length - suffix.Length;

            if (cut >= rv && cut >= MinStem && stem.EndsWith(suffix, StringComparison.Ordinal))
            {
                stem = stem[..cut];
                return true;
            }
        }

        return false;
    }
}
