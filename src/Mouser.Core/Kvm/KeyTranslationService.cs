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
        { 0x06, 0x5A }  // Z
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
