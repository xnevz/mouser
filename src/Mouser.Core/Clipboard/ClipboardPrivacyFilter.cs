using System;
using System.Collections.Generic;
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

    // D17 Detectors: JWT, Slack, Google API Key, Azure Storage Key, .env secrets, IBAN
    private static readonly Regex JwtRegex = new(@"\beyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b", RegexOptions.Compiled);
    private static readonly Regex SlackTokenRegex = new(@"\bxox[baprs]-[A-Za-z0-9-]{10,}\b", RegexOptions.Compiled);
    private static readonly Regex GoogleApiKeyRegex = new(@"\bAIza[0-9A-Za-z_-]{35}\b", RegexOptions.Compiled);
    private static readonly Regex AzureStorageKeyRegex = new(@"\bAccountKey=[A-Za-z0-9+/=]{60,}\b", RegexOptions.Compiled);
    private static readonly Regex DotEnvSecretRegex = new(@"^[ \t]*[A-Za-z0-9_]*(?:SECRET|TOKEN|PASSWORD|API_KEY)[A-Za-z0-9_]*\s*=\s*['""]?[^\s'""]{8,}['""]?\s*$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly Regex IbanCandidateRegex = new(@"\b[A-Za-z]{2}\d{2}(?:[ -]?[0-9A-Za-z]){11,30}\b", RegexOptions.Compiled);

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
        if (JwtRegex.IsMatch(text)) return true;
        if (SlackTokenRegex.IsMatch(text)) return true;
        if (GoogleApiKeyRegex.IsMatch(text)) return true;
        if (AzureStorageKeyRegex.IsMatch(text)) return true;
        if (DotEnvSecretRegex.IsMatch(text)) return true;

        var ccMatch = CreditCardRegex.Match(text);
        if (ccMatch.Success && IsValidLuhn(ccMatch.Value))
        {
            return true;
        }

        if (ContainsValidIban(text)) return true;

        if (IsHighEntropyToken(text)) return true;

        return false;
    }

    /// <summary>
    /// Checks whether an application is in the ignored apps list (e.g., password managers).
    /// Comparison is case-insensitive and ignores trailing .exe extensions.
    /// </summary>
    public static bool IsIgnoredApp(string? app, IEnumerable<string>? ignored)
    {
        if (string.IsNullOrWhiteSpace(app) || ignored == null) return false;
        string normalized = app.Trim();
        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^4];

        foreach (var item in ignored)
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            string itemNorm = item.Trim();
            if (itemNorm.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                itemNorm = itemNorm[..^4];

            if (string.Equals(normalized, itemNorm, StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// Validates an IBAN according to ISO 13616 format and ISO 7064 mod 97 checksum.
    /// </summary>
    public static bool IsValidIban(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        string clean = candidate.Replace(" ", "").Replace("-", "").ToUpperInvariant();
        if (clean.Length < 15 || clean.Length > 34) return false;

        // Country code must be 2 uppercase ASCII letters
        if (!char.IsAsciiLetterUpper(clean[0]) || !char.IsAsciiLetterUpper(clean[1])) return false;
        // Check digits must be 2 ASCII digits
        if (!char.IsAsciiDigit(clean[2]) || !char.IsAsciiDigit(clean[3])) return false;

        // Remaining characters must be ASCII alphanumeric
        for (int i = 4; i < clean.Length; i++)
        {
            if (!char.IsAsciiLetterOrDigit(clean[i])) return false;
        }

        // ISO 7064 mod 97: move first 4 characters to end, convert letters to numbers (A=10..Z=35), check mod 97 == 1
        int remainder = 0;
        for (int i = 4; i < clean.Length; i++)
        {
            char c = clean[i];
            if (char.IsAsciiDigit(c))
            {
                remainder = (remainder * 10 + (c - '0')) % 97;
            }
            else
            {
                int val = c - 'A' + 10;
                remainder = (remainder * 100 + val) % 97;
            }
        }

        for (int i = 0; i < 4; i++)
        {
            char c = clean[i];
            if (char.IsAsciiDigit(c))
            {
                remainder = (remainder * 10 + (c - '0')) % 97;
            }
            else
            {
                int val = c - 'A' + 10;
                remainder = (remainder * 100 + val) % 97;
            }
        }

        return remainder == 1;
    }

    private static bool ContainsValidIban(string text)
    {
        if (IsValidIban(text.Trim())) return true;

        foreach (Match match in IbanCandidateRegex.Matches(text))
        {
            if (IsValidIban(match.Value)) return true;
        }
        return false;
    }

    /// <summary>
    /// Computes the Shannon entropy (in bits per character) of a string.
    /// </summary>
    public static double CalculateShannonEntropy(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0.0;

        var counts = new Dictionary<char, int>();
        foreach (char c in s)
        {
            counts[c] = counts.GetValueOrDefault(c) + 1;
        }

        double entropy = 0.0;
        double len = s.Length;
        foreach (int count in counts.Values)
        {
            double p = count / len;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }

    private static bool IsHighEntropyToken(string text)
    {
        string token = text.Trim();
        if (token.Length < 24) return false;

        // ONLY when the text is a single token with NO whitespace/spaces
        for (int i = 0; i < token.Length; i++)
        {
            if (char.IsWhiteSpace(token[i])) return false;
        }

        // Standard URLs must stay non-sensitive
        if (token.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            (Uri.TryCreate(token, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
        {
            return false;
        }

        // UUIDs must stay non-sensitive
        if (Guid.TryParse(token, out _)) return false;

        // Mixed character classes: at least 3 of uppercase, lowercase, digit, symbol/punctuation
        bool hasUpper = false;
        bool hasLower = false;
        bool hasDigit = false;
        bool hasSymbol = false;

        foreach (char c in token)
        {
            if (char.IsAsciiLetterUpper(c)) hasUpper = true;
            else if (char.IsAsciiLetterLower(c)) hasLower = true;
            else if (char.IsAsciiDigit(c)) hasDigit = true;
            else if (char.IsPunctuation(c) || char.IsSymbol(c)) hasSymbol = true;
        }

        int classCount = (hasUpper ? 1 : 0) + (hasLower ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSymbol ? 1 : 0);
        if (classCount < 3) return false;

        return CalculateShannonEntropy(token) >= 4.0;
    }
}
