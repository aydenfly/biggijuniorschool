using System.ComponentModel.DataAnnotations;

namespace Bjs.Cms.Contracts;

/// <summary>Marks a string property as rich text. It is run through the HTML allow-list sanitiser on save.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class HtmlAttribute : Attribute { }

/// <summary>Plain text that may span several lines (addresses, etc.).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MultilineAttribute : Attribute { }

/// <summary>Maximum text length.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MaxAttribute : ValidationAttribute
{
    public int Length { get; }
    public MaxAttribute(int length) { Length = length; ErrorMessage = $"Keep this to {length} characters or fewer."; }
    public override bool IsValid(object? value) => value is not string s || s.Length <= Length;
}

/// <summary>Maximum number of items in a list.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MaxItemsAttribute : ValidationAttribute
{
    public int Count { get; }
    public MaxItemsAttribute(int count) { Count = count; ErrorMessage = $"Add no more than {count} items."; }
    public override bool IsValid(object? value) => value is not System.Collections.ICollection c || c.Count <= Count;
}

/// <summary>Required (non-blank) text.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NeedAttribute : ValidationAttribute
{
    public NeedAttribute() { ErrorMessage = "This field is required."; }
    public override bool IsValid(object? value) => value is string s && !string.IsNullOrWhiteSpace(s);
}

/// <summary>Whole number between two bounds.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IntRangeAttribute : ValidationAttribute
{
    public int Min { get; }
    public int Max { get; }
    public IntRangeAttribute(int min, int max) { Min = min; Max = max; ErrorMessage = $"Enter a number between {min} and {max}."; }
    public override bool IsValid(object? value) => value is not int i || (i >= Min && i <= Max);
}

/// <summary>
/// A link: empty, a site path (/about), an in-page anchor (#top), http(s), mailto: or tel:.
/// Rejects javascript:, data: and protocol-relative URLs.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class LinkUrlAttribute : ValidationAttribute
{
    public LinkUrlAttribute() { ErrorMessage = "Use a link that starts with https://, /, #, mailto: or tel:."; }
    public override bool IsValid(object? value)
    {
        if (value is not string s || s.Length == 0) return true;
        return UrlRules.IsSafeLink(s);
    }
}

/// <summary>An image location: empty, a site path (/uploads/x.webp) or an http(s) URL.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MediaUrlAttribute : ValidationAttribute
{
    public MediaUrlAttribute() { ErrorMessage = "Choose an image from the library, or use a path such as /images/photo.webp."; }
    public override bool IsValid(object? value)
    {
        if (value is not string s || s.Length == 0) return true;
        return UrlRules.IsSafeMedia(s);
    }
}

/// <summary>Map embed: empty or an https URL on an approved map host.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EmbedUrlAttribute : ValidationAttribute
{
    public EmbedUrlAttribute() { ErrorMessage = "Paste the Google Maps embed link (it starts with https://www.google.com/maps/embed)."; }
    public override bool IsValid(object? value)
    {
        if (value is not string s || s.Length == 0) return true;
        return UrlRules.IsApprovedEmbed(s);
    }
}

public static class UrlRules
{
    static readonly string[] EmbedHosts = ["www.google.com", "google.com", "maps.google.com", "www.openstreetmap.org"];

    public static bool IsSafeLink(string s)
    {
        if (s.Any(char.IsControl)) return false;
        if (s.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || s.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) return s.Length > 5;
        if (s.Contains(' ')) return false;
        if (s.StartsWith('#')) return true;
        if (s.StartsWith('/')) return !s.StartsWith("//") && !s.StartsWith("/\\");
        return Uri.TryCreate(s, UriKind.Absolute, out var u)
            && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp)
            && !string.IsNullOrEmpty(u.Host);
    }

    public static bool IsSafeMedia(string s)
    {
        if (s.Any(char.IsControl) || s.Contains(' ')) return false;
        if (s.StartsWith('/')) return !s.StartsWith("//") && !s.StartsWith("/\\") && !s.Contains("..");
        return Uri.TryCreate(s, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);
    }

    public static bool IsApprovedEmbed(string s) =>
        Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps &&
        EmbedHosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase) &&
        (u.AbsolutePath.StartsWith("/maps", StringComparison.OrdinalIgnoreCase) ||
         u.AbsolutePath.StartsWith("/export/embed", StringComparison.OrdinalIgnoreCase));
}
