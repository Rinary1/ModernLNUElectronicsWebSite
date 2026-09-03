namespace ModernLNUElectronicsWebSite.Search;

public enum SearchKind
{
    News,
    Staff,
    Administration,
    Partner,
}

public sealed record SearchDoc(
    string Id,
    SearchKind Kind,
    string Title,
    string Url,
    string? Subtitle,
    string Text,
    DateTime? Date);

public sealed record SearchHit(SearchDoc Doc, double Score, string Snippet);
