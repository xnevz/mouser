using System;

namespace Mouser.Core.Clipboard;

/// <summary>
/// Pure parser for hotkey strings like "Win+Alt+V", "Ctrl+Shift+F1", "Alt+1".
/// Modifiers: Ctrl/Control, Alt, Shift, Win/Windows/Super.
/// Keys: A-Z, 0-9, F1-F24, Space, Tab, Enter, Esc, Backspace, Insert, Delete, Home, End, PageUp, PageDown.
/// </summary>
public static class HotkeyParser
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    public static bool TryParse(string? text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(text)) return false;

        var tokens = text.Split('+', StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return false;

        uint mods = 0;
        uint vk = 0;
        bool keyFound = false;

        foreach (var token in tokens)
        {
            if (string.IsNullOrEmpty(token)) return false;

            if (string.Equals(token, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Control", StringComparison.OrdinalIgnoreCase))
            {
                mods |= MOD_CONTROL;
            }
            else if (string.Equals(token, "Alt", StringComparison.OrdinalIgnoreCase))
            {
                mods |= MOD_ALT;
            }
            else if (string.Equals(token, "Shift", StringComparison.OrdinalIgnoreCase))
            {
                mods |= MOD_SHIFT;
            }
            else if (string.Equals(token, "Win", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "Windows", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "Super", StringComparison.OrdinalIgnoreCase))
            {
                mods |= MOD_WIN;
            }
            else
            {
                if (keyFound) return false;

                if (!TryParseKey(token, out vk))
                {
                    return false;
                }
                keyFound = true;
            }
        }

        if (!keyFound) return false;

        modifiers = mods;
        virtualKey = vk;
        return true;
    }

    private static bool TryParseKey(string token, out uint vk)
    {
        vk = 0;
        if (string.IsNullOrEmpty(token)) return false;

        // Letters: A-Z
        if (token.Length == 1 && char.IsAsciiLetter(token[0]))
        {
            vk = (uint)char.ToUpperInvariant(token[0]);
            return true;
        }

        // Digits: 0-9
        if (token.Length == 1 && char.IsAsciiDigit(token[0]))
        {
            vk = (uint)token[0];
            return true;
        }

        // Function keys: F1 - F24
        if (token.StartsWith('F') || token.StartsWith('f'))
        {
            if (int.TryParse(token.AsSpan(1), out int fIndex) && fIndex >= 1 && fIndex <= 24)
            {
                vk = (uint)(0x70 + (fIndex - 1)); // VK_F1 = 0x70
                return true;
            }
        }

        switch (token.ToUpperInvariant())
        {
            case "SPACE": vk = 0x20; return true;
            case "TAB": vk = 0x09; return true;
            case "ENTER":
            case "RETURN": vk = 0x0D; return true;
            case "ESC":
            case "ESCAPE": vk = 0x1B; return true;
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
            default: return false;
        }
    }
}
