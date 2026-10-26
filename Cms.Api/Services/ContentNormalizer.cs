using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Bjs.Cms.Contracts;

namespace Bjs.Cms.Api.Services;

/// <summary>
/// Walks a content object and (1) cleans every string (rich text goes through the sanitiser, plain text loses
/// control characters and stray whitespace) and (2) validates it against the attributes declared in Cms.Contracts.
/// Errors come back keyed by the JSON path of the offending field (e.g. "people[2].bio.title"), so the admin UI
/// can show them next to the right input.
/// </summary>
public static class ContentNormalizer
{
    public static Dictionary<string, string[]> Normalize(object root)
    {
        var errors = new Dictionary<string, List<string>>();
        Walk(root, "", errors);
        return errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    static string Name(PropertyInfo p) => JsonNamingPolicy.CamelCase.ConvertName(p.Name);

    static void Walk(object obj, string path, Dictionary<string, List<string>> errors)
    {
        foreach (var p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || !p.CanWrite || p.GetIndexParameters().Length > 0) continue;
            var here = path.Length == 0 ? Name(p) : path + "." + Name(p);
            var t = p.PropertyType;

            if (t == typeof(string))
            {
                var raw = (string?)p.GetValue(obj) ?? "";
                var cleaned = p.IsDefined(typeof(HtmlAttribute))
                    ? HtmlSanitizer.Clean(raw)
                    : CleanText(raw, p.IsDefined(typeof(MultilineAttribute)));
                p.SetValue(obj, cleaned);
                Check(p, cleaned, here, errors);
            }
            else if (t == typeof(List<string>))
            {
                var list = ((List<string>?)p.GetValue(obj) ?? new()).Select(s => CleanText(s ?? "", false)).Where(s => s.Length > 0).ToList();
                p.SetValue(obj, list);
                Check(p, list, here, errors);
            }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var itemType = t.GetGenericArguments()[0];
                var list = (IList?)p.GetValue(obj);
                if (list == null) { list = (IList)Activator.CreateInstance(t)!; p.SetValue(obj, list); }
                for (int n = list.Count - 1; n >= 0; n--) if (list[n] == null) list.RemoveAt(n);
                if (itemType.IsClass && itemType != typeof(string))
                    for (int n = 0; n < list.Count; n++) Walk(list[n]!, $"{here}[{n}]", errors);
                Check(p, list, here, errors);
            }
            else if (t.IsClass)
            {
                var child = p.GetValue(obj);
                if (child == null) { child = Activator.CreateInstance(t)!; p.SetValue(obj, child); }
                Walk(child, here, errors);
            }
            else Check(p, p.GetValue(obj), here, errors);
        }
    }

    static void Check(PropertyInfo p, object? value, string path, Dictionary<string, List<string>> errors)
    {
        foreach (var attr in p.GetCustomAttributes<ValidationAttribute>(true))
        {
            if (attr.IsValid(value)) continue;
            if (!errors.TryGetValue(path, out var list)) errors[path] = list = new();
            list.Add(attr.ErrorMessage ?? "Invalid value.");
        }
    }

    /// <summary>Removes control characters; collapses whitespace unless the field is multi-line.</summary>
    public static string CleanText(string s, bool multiline)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch == '\n' && multiline) sb.Append('\n');
            else if (ch == '\r') continue;
            else if (char.IsControl(ch)) sb.Append(' ');
            else sb.Append(ch);
        }
        var text = sb.ToString();
        if (multiline)
            return string.Join("\n", text.Split('\n').Select(l => l.Trim())).Trim();
        return System.Text.RegularExpressions.Regex.Replace(text, @"[ ]{2,}", " ").Trim();
    }
}
