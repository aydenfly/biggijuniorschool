using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bjs.Cms.Contracts;

/// <summary>The one-page website's routes and the section each one scrolls to.</summary>
public static class SiteRoutes
{
    public record Route(string Path, string Anchor, string Label);

    public static readonly IReadOnlyList<Route> All =
    [
        new("/", "hero", "Home"),
        new("/about", "about", "About Us"),
        new("/leadership", "profiles", "Leadership"),
        new("/history", "history", "History"),
        new("/gallery", "gallery", "Gallery & Media (Social wall)"),
        new("/activities", "activities", "Activities & Facilities"),
        new("/contact", "contact", "Get In Touch"),
        new("/tuition", "tuition", "Tuition & Fees"),
        new("/privacy", "privacy", "Privacy & Cookies"),
    ];

    /// <summary>Returns the section anchor for an internal link, or null for any other link.</summary>
    public static string? AnchorFor(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var path = url.Split('#', '?')[0].TrimEnd('/');
        if (path.Length == 0) path = "/";
        return All.FirstOrDefault(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase))?.Anchor;
    }

    public static bool IsExternal(string? url) =>
        !string.IsNullOrEmpty(url) && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
}

public static class SiteHelpers
{
    static readonly Regex Variant = new(@"^(?<base>.+)-4k\.(?<ext>webp|jpe?g|png)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Turns a stored image path into a browser-ready address. Uploaded files live on the CMS host (/uploads/...),
    /// so they get the CMS public address in front. Anything else (/images/...) belongs to the website itself.
    /// </summary>
    public static string Media(string? src, string? cmsPublicBase)
    {
        if (string.IsNullOrWhiteSpace(src)) return "";
        if (src.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cmsPublicBase))
            return cmsPublicBase.TrimEnd('/') + src;
        return src;
    }

    /// <summary>
    /// Builds a srcset from the "-4k" naming convention (photo-4k.webp, photo-800.webp, photo-480.webp).
    /// Returns "" when the image does not follow the convention, so the browser just uses src.
    /// </summary>
    public static string Srcset(string? src, int[] widths, int mainWidth, string? cmsPublicBase)
    {
        if (string.IsNullOrWhiteSpace(src)) return "";
        var m = Variant.Match(src);
        if (!m.Success) return "";
        var sb = new StringBuilder();
        foreach (var w in widths.OrderBy(w => w))
            sb.Append(Media($"{m.Groups["base"].Value}-{w}.{m.Groups["ext"].Value}", cmsPublicBase)).Append(' ').Append(w).Append("w, ");
        sb.Append(Media(src, cmsPublicBase)).Append(' ').Append(mainWidth).Append('w');
        return sb.ToString();
    }

    public static string Slug(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalised = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalised)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }

    public static string ReplaceYear(string? text) => (text ?? "").Replace("{year}", DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture));

    /// <summary>"+256 755 293 211" -> "tel:+256755293211"</summary>
    public static string PhoneHref(string phone) => "tel:" + new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
}
