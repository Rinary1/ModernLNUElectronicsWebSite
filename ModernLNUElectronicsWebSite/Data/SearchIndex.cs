using System.Text.Json.Serialization;

namespace ModernLNUElectronicsWebSite.Data;

[JsonConverter(typeof(JsonStringEnumConverter<SearchKind>))]
public enum SearchKind
{
    News,
    Staff,
    Administration,
    Partner,
    Department,
    Schedule,
    Course,
    Page,
}

public sealed record SearchDoc(
    string Id,
    SearchKind Kind,
    string Title,
    string Route,
    string? SourceUrl,
    string? Subtitle,
    string Text,
    DateTime? Date);
