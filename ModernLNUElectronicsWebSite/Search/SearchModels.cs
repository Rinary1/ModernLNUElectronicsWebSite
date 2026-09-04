using ModernLNUElectronicsWebSite.Data;

namespace ModernLNUElectronicsWebSite.Search;

public sealed record SearchHit(SearchDoc Doc, double Score, string Snippet);
