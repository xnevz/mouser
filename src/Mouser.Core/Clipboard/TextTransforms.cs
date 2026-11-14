using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Mouser.Core.Clipboard;

/// <summary>
/// Pure text transform functions for clipboard content.
/// </summary>
public static class TextTransforms
{
    public static string Upper(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : s.ToUpperInvariant();

    public static string Lower(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : s.ToLowerInvariant();

    public static string TitleCase(string? s) =>
        string.IsNullOrEmpty(s)
            ? string.Empty
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

    public static string Trim(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : s.Trim();

    public static string RemoveLineBreaks(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var normalized = s.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        var sb = new StringBuilder(normalized.Length);
        bool prevSpace = false;
        foreach (char c in normalized)
        {
            if (c == ' ')
            {
                if (!prevSpace)
                {
                    sb.Append(' ');
                    prevSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                prevSpace = false;
            }
        }
        return sb.ToString().Trim();
    }

    public static string SortLines(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var lines = s.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        Array.Sort(lines, StringComparer.OrdinalIgnoreCase);
        return string.Join("\n", lines);
    }

    public static string DedupeLines(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var lines = s.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var line in lines)
        {
            if (seen.Add(line))
            {
                result.Add(line);
            }
        }
        return string.Join("\n", result);
    }

    public static bool TryJsonPretty(string? input, out string formatted)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            formatted = input ?? string.Empty;
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(input);
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                doc.WriteTo(writer);
            }
            formatted = Encoding.UTF8.GetString(ms.ToArray());
            return true;
        }
        catch
        {
            formatted = input ?? string.Empty;
            return false;
        }
    }

    public static string JsonPretty(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return TryJsonPretty(s, out var res) ? res : s;
    }

    public static string JsonMinify(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s ?? string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(s);
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
            {
                doc.WriteTo(writer);
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            return s;
        }
    }

    public static string UrlEncode(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return Uri.EscapeDataString(s);
    }

    public static string UrlDecode(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        try
        {
            return Uri.UnescapeDataString(s);
        }
        catch
        {
            return s;
        }
    }

    public static string Base64Encode(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    }

    public static string Base64Decode(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        try
        {
            var bytes = Convert.FromBase64String(s.Trim());
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return s;
        }
    }

    public static string StripFormatting(string? s) => s ?? string.Empty;

    public static IReadOnlyList<(string Name, Func<string, string> Apply)> All { get; } =
        new List<(string Name, Func<string, string> Apply)>
        {
            ("Upper", s => Upper(s)),
            ("Lower", s => Lower(s)),
            ("TitleCase", s => TitleCase(s)),
            ("Trim", s => Trim(s)),
            ("RemoveLineBreaks", s => RemoveLineBreaks(s)),
            ("SortLines", s => SortLines(s)),
            ("DedupeLines", s => DedupeLines(s)),
            ("JsonPretty", s => JsonPretty(s)),
            ("JsonMinify", s => JsonMinify(s)),
            ("UrlEncode", s => UrlEncode(s)),
            ("UrlDecode", s => UrlDecode(s)),
            ("Base64Encode", s => Base64Encode(s)),
            ("Base64Decode", s => Base64Decode(s)),
            ("StripFormatting", s => StripFormatting(s))
        };
}
