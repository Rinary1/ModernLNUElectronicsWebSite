namespace ModernLNUElectronicsWebSite.Data;

public enum ScheduleCategory
{
    Classes,

    Exams,
}

public sealed record ScheduleDoc(
    ScheduleCategory Category,
    string Section,
    string Title,
    string Url,
    string SourceUrl);

public sealed record ScheduleTable
{
    public required string Kind { get; init; }

    public required string Url { get; init; }

    public required string Title { get; init; }

    public required IReadOnlyList<string> Groups { get; init; }

    public required IReadOnlyList<ScheduleTableRow> Rows { get; init; }

    public bool IsWeekly => Kind == "weekly";
}

public sealed record ScheduleTableRow
{
    public string Label { get; init; } = string.Empty;

    public string Day { get; init; } = string.Empty;

    public string Pair { get; init; } = string.Empty;

    public string Time { get; init; } = string.Empty;

    public IReadOnlyList<string> Cells { get; init; } = [];

    public string CellOf(int column) => column >= 0 && column < Cells.Count ? Cells[column] : string.Empty;
}

public sealed record ScheduleGroupRef(
    string Group,
    string File,
    int Column,
    string Kind,
    string Title,
    string Section)
{
    public bool IsWeekly => Kind == "weekly";
}
