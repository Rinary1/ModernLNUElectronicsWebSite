namespace ModernLNUElectronicsWebSite.Data;

public static class SiteUrls
{
    public const string Origin = "https://electronics.lnu.edu.ua";

    private const string Host = "electronics.lnu.edu.ua";

    private const int MaxFileSlugLength = 80;

    private const int HashLength = 8;

    private static readonly Dictionary<string, string> StaticRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/"] = "",
        ["/news"] = "news",
        ["/about"] = "about",
        ["/about/introduction"] = "about",
        ["/about/administration"] = "administration",
        ["/about/staff"] = "staff",
        ["/about/departments"] = "departments",
        ["/students/career"] = "schedule",
        ["/students/rozklad-format-pdf"] = "schedule",
        ["/contacts"] = "contacts",
    };

    private static readonly Dictionary<string, string> MirroredPageRoutes = MirrorCatalog.Pages.ToDictionary(
        page => new Uri(page.SourceUrl).AbsolutePath.TrimEnd('/'),
        page => $"{page.Group}/{page.Slug}",
        StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> DetailRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["news"] = "news",
        ["employee"] = "staff",
        ["department"] = "departments",
        ["course"] = "courses",
    };

    public static string Slug(string url)
    {
        var path = TryGetPath(url) ?? url;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? string.Empty : segments[^1];
    }

    public static string FileName(string slug) =>
        slug.Length <= MaxFileSlugLength
            ? slug
            : $"{slug[..(MaxFileSlugLength - HashLength - 1)].TrimEnd('-')}-{Hash(slug)}";

    private static string Hash(string value)
    {
        var hash = 2166136261u;

        foreach (var c in value)
        {
            hash ^= c;
            hash *= 16777619u;
        }

        return hash.ToString("x8");
    }

    public static string PdfSlug(string url)
    {
        var path = url.Split('?')[0];
        var name = path[(path.LastIndexOf('/') + 1)..];

        var dot = name.LastIndexOf('.');
        if (dot > 0)
            name = name[..dot];

        var chars = name
            .Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '-')
            .Take(MaxFileSlugLength)
            .ToArray();

        return new string(chars);
    }

    public static string? Kind(string url)
    {
        var path = TryGetPath(url);
        if (path is null)
            return null;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? null : segments[0];
    }

    public static bool IsOriginalSite(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Host.Equals(Host, StringComparison.OrdinalIgnoreCase);

    public static string? ToMirrorRoute(string? url)
    {
        if (!IsOriginalSite(url))
            return null;

        var path = TryGetPath(url!)!.TrimEnd('/');
        if (path.Length == 0)
            path = "/";

        if (StaticRoutes.TryGetValue(path, out var staticRoute))
            return staticRoute;

        if (MirroredPageRoutes.TryGetValue(path, out var mirroredPage))
            return mirroredPage;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 2 && DetailRoutes.TryGetValue(segments[0], out var prefix))
            return $"{prefix}/{segments[1]}";

        return null;
    }

    private static string? TryGetPath(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : null;
}
