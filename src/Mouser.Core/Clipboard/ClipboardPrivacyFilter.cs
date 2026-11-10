using System;
using System.Text.RegularExpressions;

namespace Mouser.Core.Clipboard;

/// <summary>
/// Firewall that inspects clipboard text to prevent inadvertent leaking of passwords,
/// credential vault items, credit cards, and API secrets.
/// </summary>
public static partial class ClipboardPrivacyFilter
{
    // Common API Token & Secret Regex Patterns
    private static readonly Regex GitHubTokenRegex = new(@"\b(ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9_]{36,255}\b", RegexOptions.Compiled);
    private static readonly Regex GitHubPatRegex = new(@"\bgithub_pat_[A-Za-z0-9_]{82}\b", RegexOptions.Compiled);
    private static readonly Regex AwsKeyRegex = new(@"\b(AKIA|ABIA|ACCA|ASIA)[0-9A-Z]{16}\b", RegexOptions.Compiled);
    private static readonly Regex OpenAiKeyRegex = new(@"\bsk-[A-Za-z0-9_-]{32,}\b", RegexOptions.Compiled);
    private static readonly Regex StripeSecretRegex = new(@"\b(sk_live|rk_live)_[0-9a-zA-Z]{24,}\b", RegexOptions.Compiled);
    private static readonly Regex PrivateKeyRegex = new(@"-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.Compiled);
    private static readonly Regex CreditCardRegex = new(@"\b(?:\d{4}[ -]?){3}\d{4}\b", RegexOptions.Compiled);

    /// <summary>
    /// Checks whether the content matches any sensitive pattern that should be excluded from network sync.
    /// </summary>
    public static bool IsSensitive(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        if (PrivateKeyRegex.IsMatch(text)) return true;
        if (GitHubTokenRegex.IsMatch(text)) return true;
        if (GitHubPatRegex.IsMatch(text)) return true;
        if (AwsKeyRegex.IsMatch(text)) return true;
        if (OpenAiKeyRegex.IsMatch(text)) return true;
        if (StripeSecretRegex.IsMatch(text)) return true;

        var ccMatch = CreditCardRegex.Match(text);
        if (ccMatch.Success && IsValidLuhn(ccMatch.Value))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Masks sensitive tokens for safe display in UI or logs.
    /// </summary>
    public static string Mask(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        if (text.Length <= 8)
        {
            return new string('•', text.Length);
        }

        return string.Concat(text.AsSpan(0, 4), "••••••••", text.AsSpan(text.Length - 4));
    }

    private static bool IsValidLuhn(string numberStr)
    {
        ReadOnlySpan<char> digits = numberStr.Replace(" ", "").Replace("-", "");
        if (digits.Length != 16) return false;

        int sum = 0;
        bool alternate = false;

        for (int i = digits.Length - 1; i >= 0; i--)
        {
            char c = digits[i];
            if (!char.IsDigit(c)) return false;

            int n = c - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }

        return (sum % 10 == 0);
    }
}
