namespace WebSite.Data;

public sealed record EmployeeProfile
{
    public required string ProfileUrl { get; init; }

    public required string Slug { get; init; }

    public required string FullName { get; init; }

    public string? PhotoUrl { get; init; }

    public string? PositionText { get; init; }

    public string? DepartmentUrl { get; init; }

    public string? AcademicDegree { get; init; }

    public string? AcademicTitle { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public IReadOnlyDictionary<string, string> Profiles { get; init; } =
        new Dictionary<string, string>();

    public string? ResearchInterests { get; init; }

    public IReadOnlyList<CourseLink> Courses { get; init; } = Array.Empty<CourseLink>();

    public IReadOnlyList<ContentSection> Sections { get; init; } = Array.Empty<ContentSection>();
}

public sealed record CourseLink(string Title, string Url);
