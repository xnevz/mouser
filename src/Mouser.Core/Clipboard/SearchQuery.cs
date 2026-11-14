using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Mouser.Core.Clipboard;

public sealed record SearchQuery(
    string Text,
    string? Type,
    string? App,
    string? Device,
    string? Tag,
    DateTime? Before,
    DateTime? After)
{
    private static readonly HashSet<string> KnownTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image", "text", "link", "code", "file", "folder"
    };

    public static SearchQuery Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new SearchQuery(string.Empty, null, null, null, null, null, null);
        }

        var tokens = Tokenize(input);
        if (tokens.Count == 0)
        {
            return new SearchQuery(string.Empty, null, null, null, null, null, null);
        }

        var freeTextParts = new List<string>();
        string? type = null;
        string? app = null;
        string? device = null;
        string? tag = null;
        DateTime? before = null;
        DateTime? after = null;

        foreach (var token in tokens)
        {
            if (token.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
            {
                var val = StripQuotes(token.Substring(5).Trim());
                if (KnownTypes.Contains(val))
                {
                    type = val.ToLowerInvariant();
                }
                else
                {
                    freeTextParts.Add(token);
                }
            }
            else if (token.StartsWith("app:", StringComparison.OrdinalIgnoreCase))
            {
                var val = StripQuotes(token.Substring(4).Trim());
                if (!string.IsNullOrEmpty(val))
                {
                    app = val;
                }
                else
                {
                    freeTextParts.Add(token);
                }
            }
            else if (token.StartsWith("device:", StringComparison.OrdinalIgnoreCase))
            {
                var val = StripQuotes(token.Substring(7).Trim());
                if (!string.IsNullOrEmpty(val))
                {
                    device = val;
                }
                else
                {
                    freeTextParts.Add(token);
                }
            }
            else if (token.StartsWith("@", StringComparison.Ordinal))
            {
                var val = StripQuotes(token.Substring(1).Trim());
                if (!string.IsNullOrEmpty(val))
                {
                    device = val;
                }
                else
                {
                    freeTextParts.Add(token);
                }
            }
            else if (token.StartsWith("#", StringComparison.Ordinal))
            {
                var val = StripQuotes(token.Substring(1).Trim());
                if (!string.IsNullOrEmpty(val))
                {
                    tag = val;
                }
                else
                {
                    freeTextParts.Add(token);
                }
            }
            else if (token.StartsWith("before:", StringComparison.OrdinalIgnoreCase))
            {
                var val = StripQuotes(token.Substring(7).Trim());
                if (TryParseDate(val, out var dt))
                {
                    before = dt;
                }
                else
                {
                    freeTextParts.Add(token);
                }
            }
            else if (token.StartsWith("after:", StringComparison.OrdinalIgnoreCase))
            {
                var val = StripQuotes(token.Substring(6).Trim());
                if (TryParseDate(val, out var dt))
                {
                    after = dt;
                }
                else
                {
                    freeTextParts.Add(token);
                }
            }
            else if (token.StartsWith("on:", StringComparison.OrdinalIgnoreCase))
            {
                var val = StripQuotes(token.Substring(3).Trim());
                if (TryParseDate(val, out var dt))
                {
                    after = dt;
                    before = dt.AddDays(1);
                }
                else
                {
                    freeTextParts.Add(token);
                }
            }
            else
            {
                freeTextParts.Add(StripQuotes(token));
            }
        }

        string fullText = string.Join(" ", freeTextParts.Where(p => !string.IsNullOrEmpty(p)));
        return new SearchQuery(fullText, type, app, device, tag, before, after);
    }

    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < input.Length)
        {
            while (i < input.Length && char.IsWhiteSpace(input[i]))
            {
                i++;
            }
            if (i >= input.Length) break;

            var sb = new StringBuilder();
            char? quoteChar = null;

            while (i < input.Length)
            {
                char c = input[i];

                if (quoteChar != null)
                {
                    sb.Append(c);
                    if (c == quoteChar.Value)
                    {
                        quoteChar = null;
                    }
                    i++;
                }
                else
                {
                    if (char.IsWhiteSpace(c))
                    {
                        break;
                    }

                    sb.Append(c);
                    if (c is '"' or '\'')
                    {
                        quoteChar = c;
                    }
                    i++;
                }
            }

            if (sb.Length > 0)
            {
                tokens.Add(sb.ToString());
            }
        }

        return tokens;
    }

    private static string StripQuotes(string text)
    {
        if (text.Length >= 2 &&
            ((text.StartsWith('"') && text.EndsWith('"')) ||
             (text.StartsWith('\'') && text.EndsWith('\''))))
        {
            return text.Substring(1, text.Length - 2);
        }
        return text;
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ||
            DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
        {
            date = dt.Date;
            return true;
        }
        date = default;
        return false;
    }
}
