using System;
using System.Collections.Generic;

namespace Mouser.Core.Kvm;

/// <summary>
/// Provides bi-directional keycode translation between macOS (Carbon/Cocoa) and Windows Virtual-Keys (VK),
/// along with key-state tracking to eliminate stuck keys on machine boundary transitions.
/// </summary>
public sealed class KeyTranslationService
{
    // macOS Carbon Keycodes
    public const uint MacKey_Return = 0x24;
    public const uint MacKey_Tab = 0x30;
    public const uint MacKey_Space = 0x31;
    public const uint MacKey_Delete = 0x33;
    public const uint MacKey_Escape = 0x35;
    public const uint MacKey_Command = 0x37;
    public const uint MacKey_Shift = 0x38;
    public const uint MacKey_Option = 0x3A;
    public const uint MacKey_Control = 0x3B;
    public const uint MacKey_LeftArrow = 0x7B;
    public const uint MacKey_RightArrow = 0x7C;
    public const uint MacKey_DownArrow = 0x7D;
    public const uint MacKey_UpArrow = 0x7E;

    // Windows Virtual Key codes
    public const uint WinVk_Back = 0x08;
    public const uint WinVk_Tab = 0x09;
    public const uint WinVk_Return = 0x0D;
    public const uint WinVk_Shift = 0x10;
    public const uint WinVk_Control = 0x11;
    public const uint WinVk_Menu = 0x12; // Alt
    public const uint WinVk_Escape = 0x1B;
    public const uint WinVk_Space = 0x20;
    public const uint WinVk_Left = 0x25;
    public const uint WinVk_Up = 0x26;
    public const uint WinVk_Right = 0x27;
    public const uint WinVk_Down = 0x28;
    public const uint WinVk_LWin = 0x5B;

    private static readonly Dictionary<uint, uint> MacToWindowsMap = new()
    {
        { MacKey_Return, WinVk_Return },
        { MacKey_Tab, WinVk_Tab },
        { MacKey_Space, WinVk_Space },
        { MacKey_Delete, WinVk_Back },
        { MacKey_Escape, WinVk_Escape },
        { MacKey_Command, WinVk_Control }, // macOS Cmd -> Windows Ctrl (Copy/Paste/Shortcut parity)
        { MacKey_Shift, WinVk_Shift },
        { MacKey_Option, WinVk_Menu },     // macOS Opt -> Windows Alt
        { MacKey_Control, WinVk_Control },
        { MacKey_LeftArrow, WinVk_Left },
        { MacKey_RightArrow, WinVk_Right },
        { MacKey_DownArrow, WinVk_Down },
        { MacKey_UpArrow, WinVk_Up },

        // Alphanumeric standard keys (A-Z)
        { 0x00, 0x41 }, // A
        { 0x0B, 0x42 }, // B
        { 0x08, 0x43 }, // C
        { 0x02, 0x44 }, // D
        { 0x0E, 0x45 }, // E
        { 0x03, 0x46 }, // F
        { 0x05, 0x47 }, // G
        { 0x04, 0x48 }, // H
        { 0x22, 0x49 }, // I
        { 0x26, 0x4A }, // J
        { 0x28, 0x4B }, // K
        { 0x25, 0x4C }, // L
        { 0x2E, 0x4D }, // M
        { 0x2D, 0x4E }, // N
        { 0x1F, 0x4F }, // O
        { 0x23, 0x50 }, // P
        { 0x0C, 0x51 }, // Q
        { 0x0F, 0x52 }, // R
        { 0x01, 0x53 }, // S
        { 0x11, 0x54 }, // T
        { 0x20, 0x55 }, // U
        { 0x09, 0x56 }, // V
        { 0x0D, 0x57 }, // W
        { 0x07, 0x58 }, // X
        { 0x10, 0x59 }, // Y
        { 0x06, 0x5A }, // Z

        // Digits 0-9
        { 0x1D, 0x30 }, { 0x12, 0x31 }, { 0x13, 0x32 }, { 0x14, 0x33 }, { 0x15, 0x34 },
        { 0x17, 0x35 }, { 0x16, 0x36 }, { 0x1A, 0x37 }, { 0x1C, 0x38 }, { 0x19, 0x39 },

        // Keypad 0-9 and operators
        { 0x52, 0x60 }, { 0x53, 0x61 }, { 0x54, 0x62 }, { 0x55, 0x63 }, { 0x56, 0x64 },
        { 0x57, 0x65 }, { 0x58, 0x66 }, { 0x59, 0x67 }, { 0x5B, 0x68 }, { 0x5C, 0x69 },
        { 0x43, 0x6A }, // Keypad *
        { 0x45, 0x6B }, // Keypad +
        { 0x4E, 0x6D }, // Keypad -
        { 0x41, 0x6E }, // Keypad .
        { 0x4B, 0x6F }, // Keypad /

        // Navigation
        { 0x75, 0x2E }, // Forward delete
        { 0x73, 0x24 }, // Home
        { 0x77, 0x23 }, // End
        { 0x74, 0x21 }, // Page up
        { 0x79, 0x22 }  // Page down
    };

    private static readonly Dictionary<uint, uint> WindowsToMacMap = new();

    static KeyTranslationService()
    {
        foreach (var (macKey, winKey) in MacToWindowsMap)
        {
            WindowsToMacMap.TryAdd(winKey, macKey);
        }
    }

    private readonly HashSet<uint> _currentlyHeldKeys = new();
    private readonly object _lock = new();

    /// <summary>
    /// Translates a macOS keycode into the corresponding Windows Virtual Key.
    /// </summary>
    public uint TranslateMacToWindows(uint macKeycode)
    {
        return MacToWindowsMap.TryGetValue(macKeycode, out var winKey) ? winKey : macKeycode;
    }

    /// <summary>
    /// Translates a Windows Virtual Key into the corresponding macOS keycode.
    /// </summary>
    public uint TranslateWindowsToMac(uint winKeycode)
    {
        return WindowsToMacMap.TryGetValue(winKeycode, out var macKey) ? macKey : winKeycode;
    }

    /// <summary>
    /// Translates a Windows Virtual Key into a macOS keycode; false when there is no mapping
    /// (injecting the raw value would press an unrelated key).
    /// </summary>
    public bool TryTranslateWindowsToMac(uint winKeycode, out uint macKeycode)
    {
        return WindowsToMacMap.TryGetValue(winKeycode, out macKeycode);
    }

    /// <summary>
    /// Translates bitmask modifiers (Bit 0: Cmd, Bit 1: Shift, Bit 2: Ctrl, Bit 3: Alt)
    /// between macOS and Windows conventions.
    /// </summary>
    public byte TranslateModifiersMacToWindows(byte macModifiers)
    {
        byte winModifiers = 0;
        bool hasCmd = (macModifiers & 0x01) != 0;
        bool hasShift = (macModifiers & 0x02) != 0;
        bool hasCtrl = (macModifiers & 0x04) != 0;
        bool hasOpt = (macModifiers & 0x08) != 0;

        if (hasCmd || hasCtrl) winModifiers |= 0x04; // Ctrl on Windows
        if (hasShift) winModifiers |= 0x02;          // Shift on Windows
        if (hasOpt) winModifiers |= 0x08;            // Alt on Windows

        return winModifiers;
    }

    /// <summary>
    /// Tracks key presses and releases.
    /// </summary>
    public void RecordKeyState(uint keycode, bool isDown)
    {
        lock (_lock)
        {
            if (isDown)
            {
                _currentlyHeldKeys.Add(keycode);
            }
            else
            {
                _currentlyHeldKeys.Remove(keycode);
            }
        }
    }

    /// <summary>
    /// Returns all currently held keys and clears tracking, allowing safe synthetic key-up dispatch
    /// to prevent stuck keys across machine switches.
    /// </summary>
    public List<uint> DrainStuckKeys()
    {
        lock (_lock)
        {
            var stuckKeys = new List<uint>(_currentlyHeldKeys);
            _currentlyHeldKeys.Clear();
            return stuckKeys;
        }
    }

    public int HeldKeyCount
    {
        get
        {
            lock (_lock) return _currentlyHeldKeys.Count;
        }
    }
}
