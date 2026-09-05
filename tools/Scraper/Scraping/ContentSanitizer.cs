using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ModernLNUElectronicsWebSite.Data;

namespace ModernLNUElectronicsWebSite.Scraper.Scraping;

public static class ContentSanitizer
{
    private const int PreferredImageWidth = 1200;

    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "P", "BR", "HR",
        "STRONG", "B", "EM", "I", "U", "S", "SUP", "SUB", "MARK", "SMALL",
        "UL", "OL", "LI", "BLOCKQUOTE",
        "H2", "H3", "H4", "H5", "H6",
        "A", "IMG", "FIGURE", "FIGCAPTION",
        "TABLE", "THEAD", "TBODY", "TFOOT", "TR", "TH", "TD",
        "CODE", "PRE",
    };

    private static readonly HashSet<string> DroppedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "SCRIPT", "STYLE", "NOSCRIPT", "IFRAME", "OBJECT", "EMBED", "SVG", "CANVAS",
        "FORM", "INPUT", "BUTTON", "SELECT", "TEXTAREA", "LABEL", "NAV", "AUDIO", "VIDEO",
    };

    private static readonly Dictionary<string, string[]> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A"] = ["href"],
        ["IMG"] = ["src", "alt", "width", "height"],
        ["TD"] = ["colspan", "rowspan"],
        ["TH"] = ["colspan", "rowspan"],
    };

    private static readonly Regex SrcSetEntry = new(@"(?<url>\S+)\s+(?<width>\d+)w", RegexOptions.Compiled);

    private static readonly HtmlParser Parser = new();

    public static string Sanitize(IElement root, Uri? pageUri)
    {
        CleanChildren(root, pageUri);
        return root.InnerHtml.Trim();
    }

    public static string SanitizeFragment(string html, Uri? pageUri)
    {
        var document = Parser.ParseDocument($"<div id=\"root\">{html}</div>");
        var root = document.GetElementById("root");
        return root is null ? string.Empty : Sanitize(root, pageUri);
    }

    private static void CleanChildren(IElement parent, Uri? pageUri)
    {
        foreach (var child in parent.Children.ToArray())
            CleanElement(child, pageUri);
    }

    private static void CleanElement(IElement element, Uri? pageUri)
    {
        if (DroppedTags.Contains(element.TagName))
        {
            element.Remove();
            return;
        }

        CleanChildren(element, pageUri);

        if (!AllowedTags.Contains(element.TagName))
        {
            Unwrap(element);
            return;
        }

        NormalizeAttributes(element, pageUri);

        if (IsEmptyNoise(element))
            element.Remove();
    }

    private static void Unwrap(IElement element)
    {
        var parent = element.ParentElement;
        if (parent is null)
            return;

        while (element.FirstChild is { } child)
            parent.InsertBefore(child, element);

        element.Remove();
    }

    private static void NormalizeAttributes(IElement element, Uri? pageUri)
    {
        var keep = AllowedAttributes.TryGetValue(element.TagName, out var allowed) ? allowed : [];

        if (element.TagName.Equals("IMG", StringComparison.OrdinalIgnoreCase))
            PickImageSource(element);

        foreach (var attribute in element.Attributes.ToArray())
        {
            if (!keep.Contains(attribute.Name, StringComparer.OrdinalIgnoreCase))
            {
                element.RemoveAttribute(attribute.Name);
                continue;
            }

            if (attribute.Name is "href" or "src")
            {
                var absolute = Absolutize(attribute.Value, pageUri);
                if (absolute is null)
                    element.RemoveAttribute(attribute.Name);
                else
                    element.SetAttribute(attribute.Name, absolute);
            }
        }

        switch (element.TagName.ToUpperInvariant())
        {
            case "A" when element.GetAttribute("href") is null:
                Unwrap(element);
                break;

            case "A":
                RewriteLink(element);
                break;

            case "IMG" when element.GetAttribute("src") is null:
                element.Remove();
                break;

            case "IMG" when DeclaredWidth(element) is not null:
                element.SetAttribute("loading", "lazy");
                break;
        }
    }

    private static void RewriteLink(IElement anchor)
    {
        var href = anchor.GetAttribute("href")!;
        var mirrorRoute = SiteUrls.ToMirrorRoute(href);

        if (mirrorRoute is not null)
        {
            anchor.SetAttribute("href", mirrorRoute.Length == 0 ? "./" : mirrorRoute);
            anchor.RemoveAttribute("target");
            anchor.RemoveAttribute("rel");
            return;
        }

        anchor.SetAttribute("target", "_blank");
        anchor.SetAttribute("rel", "noopener noreferrer");
    }

    private static void PickImageSource(IElement image)
    {
        var srcset = image.GetAttribute("srcset");
        if (string.IsNullOrWhiteSpace(srcset))
            return;

        var target = DeclaredWidth(image) is { } declared
            ? Math.Min(declared * 2, PreferredImageWidth)
            : PreferredImageWidth;

        var best = SrcSetEntry.Matches(srcset)
            .Select(m => (Url: m.Groups["url"].Value, Width: int.Parse(m.Groups["width"].Value)))
            .OrderBy(c => Math.Abs(c.Width - target))
            .FirstOrDefault();

        if (best.Url is { Length: > 0 })
            image.SetAttribute("src", best.Url);
    }

    private static int? DeclaredWidth(IElement image) =>
        int.TryParse(image.GetAttribute("width"), out var width) && width > 0 ? width : null;

    private static bool IsEmptyNoise(IElement element) =>
        element.TagName is "P" or "UL" or "OL" or "LI" or "FIGURE" or "BLOCKQUOTE" or "TABLE"
            or "H2" or "H3" or "H4" or "H5" or "H6"
        && element.Children.Length == 0
        && string.IsNullOrWhiteSpace(element.TextContent);

    private static string? Absolutize(string? value, Uri? pageUri)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        if (Uri.IsWellFormedUriString(value, UriKind.Absolute))
            return value;

        if (value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
            return value;

        if (pageUri is null)
            return null;

        return Uri.TryCreate(pageUri, value, out var absolute) ? absolute.ToString() : null;
    }
}
