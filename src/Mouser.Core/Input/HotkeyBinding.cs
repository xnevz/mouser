using System;
using System.Collections.Generic;

namespace Mouser.Core.Input;

public readonly record struct HotkeyBinding(ushort Key, bool Ctrl, bool Alt, bool Shift, bool Win)
{
    public bool IsEmpty => Key == 0;

    public static bool TryParse(string? text, out HotkeyBinding b)
    {
        b = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var tokens = text.Split('+', StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return false;

        bool ctrl = false;
        bool alt = false;
        bool shift = false;
        bool win = false;
        ushort key = 0;
        bool keyFound = false;

        foreach (var token in tokens)
        {
            if (string.IsNullOrEmpty(token)) return false;

            if (string.Equals(token, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Control", StringComparison.OrdinalIgnoreCase))
            {
                ctrl = true;
            }
            else if (string.Equals(token, "Alt", StringComparison.OrdinalIgnoreCase))
            {
                alt = true;
            }
            else if (string.Equals(token, "Shift", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
            }
            else if (string.Equals(token, "Win", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "Windows", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "Super", StringComparison.OrdinalIgnoreCase))
            {
                win = true;
            }
            else
            {
                if (keyFound) return false; // multiple keys

                if (!TryParseKey(token, out key))
                {
                    return false;
                }
                keyFound = true;
            }
        }

        if (!keyFound) return false; // modifier only

        b = new HotkeyBinding(key, ctrl, alt, shift, win);
        return true;
    }

    public override string ToString()
    {
        if (IsEmpty) return string.Empty;

        var parts = new List<string>(5);
        if (Win) parts.Add("Win");
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        parts.Add(KeyToString(Key));
        return string.Join("+", parts);
    }

    public bool Matches(ushort key, IReadOnlySet<ushort> physicalKeysDown)
    {
        if (IsEmpty || key != Key) return false;

        bool hasCtrl = physicalKeysDown.Contains(0x11) || physicalKeysDown.Contains(0xA2) || physicalKeysDown.Contains(0xA3);
        bool hasAlt = physicalKeysDown.Contains(0x12) || physicalKeysDown.Contains(0xA4) || physicalKeysDown.Contains(0xA5);
        bool hasShift = physicalKeysDown.Contains(0x10) || physicalKeysDown.Contains(0xA0) || physicalKeysDown.Contains(0xA1);
        bool hasWin = physicalKeysDown.Contains(0x5B) || physicalKeysDown.Contains(0x5C);

        return Ctrl == hasCtrl && Alt == hasAlt && Shift == hasShift && Win == hasWin;
    }

    private static bool TryParseKey(string token, out ushort vk)
    {
        vk = 0;
        if (string.IsNullOrEmpty(token)) return false;

        // Letters: A-Z
        if (token.Length == 1 && char.IsAsciiLetter(token[0]))
        {
            vk = (ushort)char.ToUpperInvariant(token[0]);
            return true;
        }

        // Digits: 0-9
        if (token.Length == 1 && char.IsAsciiDigit(token[0]))
        {
            vk = (ushort)token[0];
            return true;
        }

        // Function keys: F1 - F24
        if (token.StartsWith('F') || token.StartsWith('f'))
        {
            if (int.TryParse(token.AsSpan(1), out int fIndex) && fIndex >= 1 && fIndex <= 24)
            {
                vk = (ushort)(0x70 + (fIndex - 1));
                return true;
            }
        }

        switch (token.ToUpperInvariant())
        {
            case "ESC":
            case "ESCAPE": vk = 0x1B; return true;
            case "TAB": vk = 0x09; return true;
            case "SPACE": vk = 0x20; return true;
            case "ENTER":
            case "RETURN": vk = 0x0D; return true;
            case "BACKSPACE":
            case "BACK": vk = 0x08; return true;
            case "INSERT":
            case "INS": vk = 0x2D; return true;
            case "DELETE":
            case "DEL": vk = 0x2E; return true;
            case "HOME": vk = 0x24; return true;
            case "END": vk = 0x23; return true;
            case "PAGEUP":
            case "PGUP": vk = 0x21; return true;
            case "PAGEDOWN":
            case "PGDN": vk = 0x22; return true;
            case "UP": vk = 0x26; return true;
            case "DOWN": vk = 0x28; return true;
            case "LEFT": vk = 0x25; return true;
            case "RIGHT": vk = 0x27; return true;
            case "SCROLL LOCK":
            case "SCROLLLOCK":
            case "SCROLL": vk = 0x91; return true;
            case "PAUSE": vk = 0x13; return true;
            default: return false;
        }
    }

    public static string KeyToString(ushort key) => key switch
    {
        >= 0x41 and <= 0x5A => ((char)key).ToString(),
        >= 0x30 and <= 0x39 => ((char)key).ToString(),
        >= 0x70 and <= 0x87 => $"F{key - 0x70 + 1}",
        0x1B => "Esc",
        0x09 => "Tab",
        0x20 => "Space",
        0x0D => "Enter",
        0x08 => "Backspace",
        0x2D => "Insert",
        0x2E => "Delete",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x26 => "Up",
        0x28 => "Down",
        0x25 => "Left",
        0x27 => "Right",
        0x91 => "Scroll Lock",
        0x13 => "Pause",
        _ => $"Key_{key:X2}"
    };
}
