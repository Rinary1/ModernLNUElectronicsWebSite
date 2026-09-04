namespace ModernLNUElectronicsWebSite.Data;

public sealed record ContentSection(string Title, string Html);

public sealed record MirrorPage
{
    public required string Slug { get; init; }

    public required string SourceUrl { get; init; }

    public required string Title { get; init; }

    public string? RawDate { get; init; }

    public DateTime? PublishedAt { get; init; }

    public string? CoverImageUrl { get; init; }

    public required string BodyHtml { get; init; }

    public required string PlainText { get; init; }
}
