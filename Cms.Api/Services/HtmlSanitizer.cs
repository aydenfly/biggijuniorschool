using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Bjs.Cms.Contracts;

namespace Bjs.Cms.Api.Services;

/// <summary>
/// Small allow-list sanitiser for rich text. Everything not explicitly allowed is removed (tags) or escaped (text),
/// so stored content can be rendered with Html.Raw on the public site without opening the door to script injection.
/// It is a tokeniser rather than a regex rewrite, and always returns balanced HTML.
/// </summary>
public static class HtmlSanitizer
{
    static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        { "p", "br", "strong", "b", "em", "i", "u", "a", "ul", "ol", "li", "h2", "h3", "h4", "blockquote" };

    static readonly HashSet<string> Void = new(StringComparer.OrdinalIgnoreCase) { "br" };

    // tags whose *content* must go too
    static readonly HashSet<string> DropWithContent = new(StringComparer.OrdinalIgnoreCase)
        { "script", "style", "iframe", "object", "embed", "noscript", "template", "svg", "math", "textarea", "title", "head" };

    static readonly Regex Entity = new(@"^&(#\d{1,7}|#[xX][0-9a-fA-F]{1,6}|[a-zA-Z][a-zA-Z0-9]{1,31});", RegexOptions.Compiled);

    public static string Clean(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var s = html;
        var sb = new StringBuilder(s.Length);
        var open = new List<string>();
        int i = 0;

        while (i < s.Length)
        {
            char c = s[i];

            if (c == '<')
            {
                // comments
                if (string.CompareOrdinal(s, i, "<!--", 0, 4) == 0)
                {
                    int end = s.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    i = end < 0 ? s.Length : end + 3;
                    continue;
                }
                // doctype / processing instructions / CDATA
                if (i + 1 < s.Length && (s[i + 1] == '!' || s[i + 1] == '?'))
                {
                    int end = s.IndexOf('>', i);
                    i = end < 0 ? s.Length : end + 1;
                    continue;
                }

                if (TryReadTag(s, i, out var tag))
                {
                    i = tag.End;

                    if (!tag.Closing && DropWithContent.Contains(tag.Name))
                    {
                        if (!tag.SelfClosing) i = SkipPast(s, i, tag.Name);
                        continue;
                    }
                    if (!Allowed.Contains(tag.Name)) continue; // unknown tag: drop the tag, keep its text

                    var name = Canonical(tag.Name);
                    if (tag.Closing)
                    {
                        int idx = open.LastIndexOf(name);
                        if (idx < 0) continue; // stray closer
                        for (int k = open.Count - 1; k >= idx; k--) sb.Append("</").Append(open[k]).Append('>');
                        open.RemoveRange(idx, open.Count - idx);
                        continue;
                    }

                    if (Void.Contains(name)) { sb.Append("<br>"); continue; }

                    // block elements cannot sit inside a paragraph / heading: close it first
                    if (IsBlock(name))
                    {
                        while (open.Count > 0 && (open[^1] == "p" || IsHeading(open[^1])) )
                        {
                            sb.Append("</").Append(open[^1]).Append('>');
                            open.RemoveAt(open.Count - 1);
                        }
                    }

                    if (name == "a")
                    {
                        // never nest links
                        if (open.Contains("a")) continue;
                        sb.Append(OpenAnchor(tag.Attributes));
                    }
                    else sb.Append('<').Append(name).Append('>');
                    open.Add(name);
                    continue;
                }

                sb.Append("&lt;");
                i++;
                continue;
            }

            if (c == '&')
            {
                var m = Entity.Match(s.Substring(i, Math.Min(40, s.Length - i)));
                if (m.Success) { sb.Append(m.Value); i += m.Length; }
                else { sb.Append("&amp;"); i++; }
                continue;
            }

            if (c == '>') { sb.Append("&gt;"); i++; continue; }
            if (c == '\0' || (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')) { i++; continue; }

            sb.Append(c);
            i++;
        }

        for (int k = open.Count - 1; k >= 0; k--) sb.Append("</").Append(open[k]).Append('>');

        var result = sb.ToString().Trim();
        return IsBlank(result) ? "" : result;
    }

    /// <summary>True when the HTML shows nothing (for example just empty paragraphs).</summary>
    public static bool IsBlank(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return true;
        var text = Regex.Replace(html, "<[^>]*>", "");
        return string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(text).Replace('\u00a0', ' '));
    }

    // ---------------------------------------------------------------------

    static string Canonical(string name) => name.ToLowerInvariant() switch { "b" => "strong", "i" => "em", var n => n };
    static bool IsHeading(string n) => n is "h2" or "h3" or "h4";
    static bool IsBlock(string n) => n is "p" or "ul" or "ol" or "h2" or "h3" or "h4" or "blockquote";

    static string OpenAnchor(List<(string Name, string Value)> attrs)
    {
        var href = attrs.FirstOrDefault(a => a.Name.Equals("href", StringComparison.OrdinalIgnoreCase)).Value;
        if (href == null) return "<a>";
        var decoded = WebUtility.HtmlDecode(href).Trim();
        if (!UrlRules.IsSafeLink(decoded) || decoded.Length > 500) return "<a>";
        var sb = new StringBuilder("<a href=\"").Append(WebUtility.HtmlEncode(decoded)).Append('"');
        if (decoded.StartsWith("http", StringComparison.OrdinalIgnoreCase)) sb.Append(" target=\"_blank\" rel=\"noopener noreferrer\"");
        return sb.Append('>').ToString();
    }

    static int SkipPast(string s, int from, string tagName)
    {
        var close = "</" + tagName;
        int i = from;
        while (i < s.Length)
        {
            int at = s.IndexOf(close, i, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return s.Length;
            int gt = s.IndexOf('>', at);
            return gt < 0 ? s.Length : gt + 1;
        }
        return s.Length;
    }

    sealed record Tag(string Name, bool Closing, bool SelfClosing, List<(string Name, string Value)> Attributes, int End);

    static bool TryReadTag(string s, int start, out Tag tag)
    {
        tag = default!;
        int i = start + 1;
        bool closing = false;
        if (i < s.Length && s[i] == '/') { closing = true; i++; }
        int nameStart = i;
        if (i >= s.Length || !char.IsAsciiLetter(s[i])) return false;
        while (i < s.Length && (char.IsAsciiLetterOrDigit(s[i]) || s[i] == '-' || s[i] == ':')) i++;
        var name = s.Substring(nameStart, i - nameStart);

        var attrs = new List<(string, string)>();
        bool selfClosing = false;

        while (i < s.Length)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length) return false; // unterminated tag: treat '<' as text
            if (s[i] == '>') { i++; break; }
            if (s[i] == '/') { selfClosing = true; i++; continue; }

            int an = i;
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '=' && s[i] != '>' && s[i] != '/') i++;
            var attrName = s.Substring(an, i - an);
            if (attrName.Length == 0) { i++; continue; }

            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            string val = "";
            if (i < s.Length && s[i] == '=')
            {
                i++;
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                if (i < s.Length && (s[i] == '"' || s[i] == '\''))
                {
                    char q = s[i++];
                    int vs = i;
                    while (i < s.Length && s[i] != q) i++;
                    if (i >= s.Length) return false;
                    val = s.Substring(vs, i - vs);
                    i++;
                }
                else
                {
                    int vs = i;
                    while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '>') i++;
                    val = s.Substring(vs, i - vs);
                }
            }
            attrs.Add((attrName, val));
        }

        tag = new Tag(name, closing, selfClosing, attrs, i);
        return true;
    }
}
