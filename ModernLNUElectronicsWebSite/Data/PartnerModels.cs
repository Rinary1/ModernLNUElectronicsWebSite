namespace ModernLNUElectronicsWebSite.Data;

public sealed record Partner
{
    public required string Name { get; init; }

    public string? Url { get; init; }

    public string? LogoUrl { get; init; }

    public required string Description { get; init; }
}
