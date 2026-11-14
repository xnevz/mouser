using System;
using System.Globalization;
using System.Text;

namespace Mouser.Core.Clipboard;

public static class SnippetExpander
{
    public static (string Text, int? CursorOffset) Expand(
        string body,
        string clipboard,
        Func<string, string?>? prompt = null,
        DateTime? now = null)
    {
        if (string.IsNullOrEmpty(body))
        {
            return (string.Empty, null);
        }

        var dt = now ?? DateTime.Now;
        var sb = new StringBuilder();
        int? firstCursorPos = null;

        int i = 0;
        while (i < body.Length)
        {
            if (i + 1 < body.Length && body[i] == '{' && body[i + 1] == '{')
            {
                // If there's a third '{' (e.g. "{{{date}}}"), output the first '{' and continue
                // so that the next token scanned is "{{date}}".
                if (i + 2 < body.Length && body[i + 2] == '{')
                {
                    sb.Append('{');
                    i++;
                    continue;
                }

                int closeIndex = body.IndexOf("}}", i + 2, StringComparison.Ordinal);
                if (closeIndex != -1)
                {
                    // Check if there is an unclosed "{{" between i + 2 and closeIndex
                    int nextOpen = body.IndexOf("{{", i + 2, closeIndex - (i + 2), StringComparison.Ordinal);
                    if (nextOpen != -1)
                    {
                        // Emit literally up to nextOpen and continue from nextOpen
                        sb.Append(body.AsSpan(i, nextOpen - i));
                        i = nextOpen;
                        continue;
                    }

                    string token = body.Substring(i + 2, closeIndex - (i + 2));
                    if (TryExpandToken(token, dt, clipboard, prompt, ref firstCursorPos, sb))
                    {
                        i = closeIndex + 2;
                        continue;
                    }
                    else
                    {
                        // Unknown variable stays literal
                        sb.Append("{{").Append(token).Append("}}");
                        i = closeIndex + 2;
                        continue;
                    }
                }
                else
                {
                    // No matching "}}"
                    sb.Append(body.AsSpan(i));
                    break;
                }
            }
            else
            {
                sb.Append(body[i]);
                i++;
            }
        }

        int? cursorOffset = firstCursorPos.HasValue
            ? sb.Length - firstCursorPos.Value
            : null;

        return (sb.ToString(), cursorOffset);
    }

    private static bool TryExpandToken(
        string token,
        DateTime dt,
        string? clipboard,
        Func<string, string?>? prompt,
        ref int? firstCursorPos,
        StringBuilder sb)
    {
        var trimmed = token.Trim();

        if (trimmed.Equals("date", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            return true;
        }

        if (trimmed.Equals("time", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(dt.ToString("HH:mm", CultureInfo.InvariantCulture));
            return true;
        }

        if (trimmed.Equals("datetime", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            return true;
        }

        if (trimmed.Equals("clipboard", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(clipboard ?? string.Empty);
            return true;
        }

        if (trimmed.Equals("uuid", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(Guid.NewGuid().ToString());
            return true;
        }

        if (trimmed.Equals("cursor", StringComparison.OrdinalIgnoreCase))
        {
            if (!firstCursorPos.HasValue)
            {
                firstCursorPos = sb.Length;
            }
            return true;
        }

        if (trimmed.StartsWith("prompt:", StringComparison.OrdinalIgnoreCase))
        {
            string label = trimmed.Substring(7);
            string? res = prompt?.Invoke(label);
            sb.Append(res ?? string.Empty);
            return true;
        }

        return false;
    }
}
