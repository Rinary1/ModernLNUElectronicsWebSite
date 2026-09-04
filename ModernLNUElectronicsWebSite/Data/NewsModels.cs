namespace ModernLNUElectronicsWebSite.Data;

public sealed record NewsItem
{
    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string Url { get; init; }

    public string? Excerpt { get; init; }

    public DateTime? PublishedAt { get; init; }

    public string? RawDate { get; init; }

    public string? CoverImageUrl { get; init; }
}

public sealed record NewsPage
{
    public required IReadOnlyList<NewsItem> Items { get; init; }

    public string? NextPageUrl { get; init; }
}
