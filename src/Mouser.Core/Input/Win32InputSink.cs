using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Mouser.Core.Input;

/// <summary>
/// Windows input injection through user32 SendInput. Needs no native library.
/// Note: Windows blocks injection into windows of elevated (admin) processes unless Mouser itself runs elevated (UIPI).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class Win32InputSink : IInputSink
{
    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_HWHEEL = 0x1000;

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint MAPVK_VK_TO_VSC = 0;

    public bool IsSupported => true;

    public void MoveRelative(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return;
        SendMouse(dx, dy, 0, MOUSEEVENTF_MOVE);
    }

    public void SetButton(MouseButton button, bool isDown)
    {
        uint flags = button switch
        {
            MouseButton.Left => isDown ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP,
            MouseButton.Right => isDown ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
            MouseButton.Middle => isDown ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
            _ => 0
        };
        if (flags != 0) SendMouse(0, 0, 0, flags);
    }

    public void Scroll(int horizontal, int vertical)
    {
        if (vertical != 0) SendMouse(0, 0, vertical, MOUSEEVENTF_WHEEL);
        if (horizontal != 0) SendMouse(0, 0, horizontal, MOUSEEVENTF_HWHEEL);
    }

    public void Key(ushort virtualKey, bool isDown)
    {
        if (virtualKey == 0 || virtualKey > 0xFE) return;

        uint flags = isDown ? 0 : KEYEVENTF_KEYUP;
        if (IsExtendedKey(virtualKey)) flags |= KEYEVENTF_EXTENDEDKEY;

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = (ushort)MapVirtualKeyW(virtualKey, MAPVK_VK_TO_VSC),
                    dwFlags = flags
                }
            }
        };
        Send(input);
    }

    public static bool IsExtendedKey(ushort vk) => vk switch
    {
        >= 0x21 and <= 0x28 => true, // PageUp, PageDown, End, Home, arrows
        0x2D or 0x2E => true,        // Insert, Delete
        0x5B or 0x5C or 0x5D => true, // Win keys, Apps
        0x6F => true,                // Numpad divide
        0x90 => true,                // NumLock
        0xA3 or 0xA5 => true,        // Right Ctrl, Right Alt
        >= 0xA6 and <= 0xB7 => true, // Browser, volume and media keys
        _ => false
    };

    private static void SendMouse(int dx, int dy, int data, uint flags)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion
            {
                mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = data, dwFlags = flags }
            }
        };
        Send(input);
    }

    private static unsafe void Send(INPUT input)
    {
        SendInput(1, &input, sizeof(INPUT));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static unsafe partial uint SendInput(uint nInputs, INPUT* pInputs, int cbSize);

    [LibraryImport("user32.dll")]
    private static partial uint MapVirtualKeyW(uint uCode, uint uMapType);
}
