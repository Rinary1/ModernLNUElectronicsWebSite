namespace ModernLNUElectronicsWebSite.Scraping;

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
