using System.Text.RegularExpressions;

namespace Mouser.Core.Clipboard;

/// <summary>Heuristically classifies copied text as a link, code or plain text.</summary>
public static partial class ClipboardClassifier
{
    [GeneratedRegex(@"^(https?|ftp)://\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"[;{}]\s*$|^\s*(using|import|from|#include|def|class|public|private|function|const|let|var|return|if|for|while)\b|=>|\(\)\s*[{:]", RegexOptions.Multiline)]
    private static partial Regex CodeLineRegex();

    public static ClipboardContentType Classify(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return ClipboardContentType.Text;

        string trimmed = text.Trim();
        if (!trimmed.Contains('\n') && UrlRegex().IsMatch(trimmed)) return ClipboardContentType.Link;

        var lines = trimmed.Split('\n');
        int codeLines = lines.Count(l => CodeLineRegex().IsMatch(l));
        // One strong signal on a single line, or a meaningful share of a multi-line snippet.
        if (lines.Length == 1 ? codeLines == 1 && trimmed.Length < 300 : codeLines * 3 >= lines.Length)
            return ClipboardContentType.Code;

        return ClipboardContentType.Text;
    }

    /// <summary>First non-empty line, shortened, for list titles.</summary>
    public static string Title(string text, int maxLength = 80)
    {
        string line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? string.Empty;
        return line.Length <= maxLength ? line : line[..(maxLength - 1)] + "…";
    }
}
