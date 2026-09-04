namespace ModernLNUElectronicsWebSite.Data;

public sealed record MirrorMeta
{
    public required DateTime GeneratedAt { get; init; }

    public int NewsCount { get; init; }

    public int StaffCount { get; init; }

    public int EmployeeProfileCount { get; init; }

    public int DepartmentCount { get; init; }

    public int ScheduleDocCount { get; init; }
}
