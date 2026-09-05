namespace WebSite.Data;

public sealed record AdministrationPerson
{
    public required AdministrationSection Section { get; init; }

    public required string Role { get; init; }

    public string? RoleDetail { get; init; }

    public string? Rank { get; init; }

    public required string Name { get; init; }

    public string? ProfileUrl { get; init; }
}

public enum AdministrationSection
{
    DeansOffice,
    Council,
}
