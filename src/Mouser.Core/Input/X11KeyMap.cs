using System.Collections.Generic;

namespace Mouser.Core.Input;

/// <summary>
/// Pure mapping table from Windows Virtual-Key codes (the Mouser wire format)
/// to X11 KeySyms for Linux X11 synthetic input injection (Task H6).
/// </summary>
public static class X11KeyMap
{
    // Standard X11 KeySyms (X11/keysymdef.h)
    public const uint XK_BackSpace = 0xFF08;
    public const uint XK_Tab = 0xFF09;
    public const uint XK_Return = 0xFF0D;
    public const uint XK_Escape = 0xFF1B;
    public const uint XK_Delete = 0xFFFF;
    public const uint XK_Home = 0xFF50;
    public const uint XK_Left = 0xFF51;
    public const uint XK_Up = 0xFF52;
    public const uint XK_Right = 0xFF53;
    public const uint XK_Down = 0xFF54;
    public const uint XK_Page_Up = 0xFF55;
    public const uint XK_Page_Down = 0xFF56;
    public const uint XK_End = 0xFF57;
    public const uint XK_Insert = 0xFF63;
    public const uint XK_Num_Lock = 0xFF7F;
    public const uint XK_Scroll_Lock = 0xFF14;
    public const uint XK_Caps_Lock = 0xFFE5;
    public const uint XK_Shift_L = 0xFFE1;
    public const uint XK_Shift_R = 0xFFE2;
    public const uint XK_Control_L = 0xFFE3;
    public const uint XK_Control_R = 0xFFE4;
    public const uint XK_Alt_L = 0xFFE9;
    public const uint XK_Alt_R = 0xFFEA;
    public const uint XK_Super_L = 0xFFEB;
    public const uint XK_Super_R = 0xFFEC;
    public const uint XK_space = 0x0020;

    // XF86 multimedia keys (X11/XF86keysym.h)
    public const uint XF86AudioLowerVolume = 0x1008FF11;
    public const uint XF86AudioMute = 0x1008FF12;
    public const uint XF86AudioRaiseVolume = 0x1008FF13;
    public const uint XF86AudioPlay = 0x1008FF14;
    public const uint XF86AudioStop = 0x1008FF15;
    public const uint XF86AudioPrev = 0x1008FF16;
    public const uint XF86AudioNext = 0x1008FF17;

    private static readonly Dictionary<ushort, uint> ExplicitMap = new()
    {
        // Control & editing
        [0x08] = XK_BackSpace,
        [0x09] = XK_Tab,
        [0x0D] = XK_Return,
        [0x1B] = XK_Escape,
        [0x20] = XK_space,
        [0x21] = XK_Page_Up,
        [0x22] = XK_Page_Down,
        [0x23] = XK_End,
        [0x24] = XK_Home,
        [0x25] = XK_Left,
        [0x26] = XK_Up,
        [0x27] = XK_Right,
        [0x28] = XK_Down,
        [0x2D] = XK_Insert,
        [0x2E] = XK_Delete,

        // Modifiers
        [0x10] = XK_Shift_L,
        [0xA0] = XK_Shift_L,
        [0xA1] = XK_Shift_R,
        [0x11] = XK_Control_L,
        [0xA2] = XK_Control_L,
        [0xA3] = XK_Control_R,
        [0x12] = XK_Alt_L,
        [0xA4] = XK_Alt_L,
        [0xA5] = XK_Alt_R,
        [0x5B] = XK_Super_L,
        [0x5C] = XK_Super_R,
        [0x14] = XK_Caps_Lock,
        [0x90] = XK_Num_Lock,
        [0x91] = XK_Scroll_Lock,

        // Media keys
        [0xAD] = XF86AudioMute,
        [0xAE] = XF86AudioLowerVolume,
        [0xAF] = XF86AudioRaiseVolume,
        [0xB0] = XF86AudioNext,
        [0xB1] = XF86AudioPrev,
        [0xB2] = XF86AudioStop,
        [0xB3] = XF86AudioPlay,

        // Numpad operators
        [0x6A] = 0xFFAA, // KP_Multiply
        [0x6B] = 0xFFAB, // KP_Add
        [0x6D] = 0xFFAD, // KP_Subtract
        [0x6E] = 0xFFAE, // KP_Decimal
        [0x6F] = 0xFFAF, // KP_Divide

        // Common OEM punctuation (US standard)
        [0xBA] = 0x003B, // semicolon
        [0xBB] = 0x003D, // equal
        [0xBC] = 0x002C, // comma
        [0xBD] = 0x002D, // minus
        [0xBE] = 0x002E, // period
        [0xBF] = 0x002F, // slash
        [0xC0] = 0x0060, // grave
        [0xDB] = 0x005B, // bracketleft
        [0xDC] = 0x005C, // backslash
        [0xDD] = 0x005D, // bracketright
        [0xDE] = 0x0027  // apostrophe
    };

    public static bool TryGetKeysym(ushort vk, out uint keysym)
    {
        // 1. Letters A-Z (VK 0x41 - 0x5A) -> lowercase keysym 'a'-'z' (0x61 - 0x7A)
        if (vk >= 0x41 && vk <= 0x5A)
        {
            keysym = (uint)(vk + 0x20);
            return true;
        }

        // 2. Digits 0-9 (VK 0x30 - 0x39) -> ASCII '0'-'9' (0x30 - 0x39)
        if (vk >= 0x30 && vk <= 0x39)
        {
            keysym = vk;
            return true;
        }

        // 3. Function keys F1-F24 (VK 0x70 - 0x87) -> XK_F1 (0xFFBE) - XK_F24 (0xFFD5)
        if (vk >= 0x70 && vk <= 0x87)
        {
            keysym = (uint)(0xFFBE + (vk - 0x70));
            return true;
        }

        // 4. Numpad 0-9 (VK 0x60 - 0x69) -> XK_KP_0 (0xFFB0) - XK_KP_9 (0xFFB9)
        if (vk >= 0x60 && vk <= 0x69)
        {
            keysym = (uint)(0xFFB0 + (vk - 0x60));
            return true;
        }

        // 5. Explicit dictionary map
        return ExplicitMap.TryGetValue(vk, out keysym);
    }

    public static uint VirtualKeyToKeysym(ushort vk)
    {
        return TryGetKeysym(vk, out uint keysym) ? keysym : 0;
    }
}
