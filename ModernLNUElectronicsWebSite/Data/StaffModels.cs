namespace ModernLNUElectronicsWebSite.Data;

public sealed record StaffItem
{
    public required StaffGroup Group { get; init; }

    public required string FullName { get; init; }

    public required string Position { get; init; }

    public bool IsExternal { get; init; }

    public string? Email { get; init; }

    public required string ProfileUrl { get; init; }

    public string? PhotoUrl { get; init; }
}

public sealed record StaffGroup
{
    public required StaffGroupKind Kind { get; init; }

    public required string Title { get; init; }

    public string? Url { get; init; }
}

public enum StaffGroupKind
{
    DeansOffice,
    DeansOfficeSecretariat,
    DepartmentOptoelectronics,        // optoelectronics-2
    DepartmentRadioelectronics,       // radioelectronics-and-computer-sciences
    DepartmentRadiophysics,           // radiophysics-and-computer-technologies
    DepartmentSensorElectronics,      // sensor-and-semiconductor-electronics
    DepartmentSystemDesign,           // system-design
    DepartmentPhysicalBioelectronics, // physic-and-bioelectronics
    Laboratory,
    Other,
}
