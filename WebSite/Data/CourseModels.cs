namespace WebSite.Data;

public sealed record CourseLecturer(string Name, string Slug);

public sealed record CourseRef
{
    public required string Slug { get; init; }

    public required string Title { get; init; }

    public required string SourceUrl { get; init; }

    public IReadOnlyList<CourseLecturer> Lecturers { get; init; } = Array.Empty<CourseLecturer>();
}
