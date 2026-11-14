using System;
using System.Text.RegularExpressions;

namespace Mouser.Core.Services;

/// <summary>
/// Pure semantic version comparer for release updates (Task H3).
/// Handles leading 'v' prefixes, varying segment counts (e.g. 1.2 vs 1.2.0),
/// and standard SemVer 2.0 pre-release identifiers.
/// </summary>
public static class VersionComparer
{
    public static bool IsNewer(string? candidate, string? current)
    {
        return Compare(candidate, current) > 0;
    }

    public static int Compare(string? v1, string? v2)
    {
        if (string.IsNullOrWhiteSpace(v1) && string.IsNullOrWhiteSpace(v2)) return 0;
        if (string.IsNullOrWhiteSpace(v1)) return -1;
        if (string.IsNullOrWhiteSpace(v2)) return 1;

        var parsed1 = Parse(v1);
        var parsed2 = Parse(v2);

        // 1. Compare major
        int cmp = parsed1.Major.CompareTo(parsed2.Major);
        if (cmp != 0) return cmp;

        // 2. Compare minor
        cmp = parsed1.Minor.CompareTo(parsed2.Minor);
        if (cmp != 0) return cmp;

        // 3. Compare patch
        cmp = parsed1.Patch.CompareTo(parsed2.Patch);
        if (cmp != 0) return cmp;

        // 4. Compare build/revision
        cmp = parsed1.Build.CompareTo(parsed2.Build);
        if (cmp != 0) return cmp;

        // 5. Compare pre-release
        // Rule: A normal version has higher precedence than a pre-release version
        // Example: 1.0.0 > 1.0.0-beta
        bool hasPre1 = !string.IsNullOrEmpty(parsed1.PreRelease);
        bool hasPre2 = !string.IsNullOrEmpty(parsed2.PreRelease);

        if (!hasPre1 && hasPre2) return 1;
        if (hasPre1 && !hasPre2) return -1;
        if (!hasPre1 && !hasPre2) return 0;

        // Both have pre-release strings
        return ComparePreRelease(parsed1.PreRelease, parsed2.PreRelease);
    }

    private static (int Major, int Minor, int Patch, int Build, string PreRelease) Parse(string raw)
    {
        string s = raw.Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            s = s.Substring(1).Trim();
        }

        // Split off build metadata (+) first
        int plusIdx = s.IndexOf('+');
        if (plusIdx >= 0)
        {
            s = s.Substring(0, plusIdx);
        }

        // Split off pre-release (-)
        string preRelease = string.Empty;
        int dashIdx = s.IndexOf('-');
        if (dashIdx >= 0)
        {
            preRelease = s.Substring(dashIdx + 1);
            s = s.Substring(0, dashIdx);
        }

        string[] parts = s.Split('.');
        int major = parts.Length > 0 && int.TryParse(parts[0], out int maj) ? maj : 0;
        int minor = parts.Length > 1 && int.TryParse(parts[1], out int min) ? min : 0;
        int patch = parts.Length > 2 && int.TryParse(parts[2], out int pat) ? pat : 0;
        int build = parts.Length > 3 && int.TryParse(parts[3], out int bld) ? bld : 0;

        return (major, minor, patch, build, preRelease);
    }

    private static int ComparePreRelease(string pre1, string pre2)
    {
        string[] id1 = pre1.Split('.');
        string[] id2 = pre2.Split('.');

        int len = Math.Min(id1.Length, id2.Length);
        for (int i = 0; i < len; i++)
        {
            bool isNum1 = int.TryParse(id1[i], out int n1);
            bool isNum2 = int.TryParse(id2[i], out int n2);

            if (isNum1 && isNum2)
            {
                int cmp = n1.CompareTo(n2);
                if (cmp != 0) return cmp;
            }
            else if (isNum1 && !isNum2)
            {
                return -1; // numeric identifiers have lower precedence than alphanumeric
            }
            else if (!isNum1 && isNum2)
            {
                return 1;
            }
            else
            {
                int cmp = string.Compare(id1[i], id2[i], StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
            }
        }

        return id1.Length.CompareTo(id2.Length);
    }
}
