using ModernLNUElectronicsWebSite.Data;

namespace ModernLNUElectronicsWebSite.Search;

public sealed record SearchHit(SearchDoc Doc, double Score, string Snippet);

public sealed record KindCount(SearchKind Kind, int Count);

public sealed record SearchResults(
    IReadOnlyList<SearchHit> Hits,
    IReadOnlyList<KindCount> Counts,
    int Total)
{
    public static readonly SearchResults Empty = new([], [], 0);
}
