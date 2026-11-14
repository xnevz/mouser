using System;
using System.Runtime.InteropServices;

namespace Mouser.Core.Input;

/// <summary>
/// Linux X11 synthetic input injection via libX11.so.6 and libXtst.so.6 (Task H6).
/// Detects Wayland sessions where synthetic input is blocked by security policy.
/// </summary>
public sealed class X11InputSink : IInputSink, IDisposable
{
    private IntPtr _display = IntPtr.Zero;
    private IntPtr _rootWindow = IntPtr.Zero;
    private bool _disposed;
    private readonly bool _isWayland;

    public bool IsSupported => _display != IntPtr.Zero && !_isWayland;
    public string? UnsupportedReason { get; }

    public X11InputSink()
    {
        if (!OperatingSystem.IsLinux())
        {
            UnsupportedReason = "X11InputSink is only supported on Linux.";
            return;
        }

        string sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? string.Empty;
        string waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? string.Empty;
        if (sessionType.Equals("wayland", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(waylandDisplay))
        {
            _isWayland = true;
            UnsupportedReason = "Wayland session detected: synthetic input injection is restricted. Run under X11/XWayland.";
            return;
        }

        try
        {
            _display = XOpenDisplay(IntPtr.Zero);
            if (_display != IntPtr.Zero)
            {
                _rootWindow = XDefaultRootWindow(_display);
            }
            else
            {
                UnsupportedReason = "Unable to open X11 display connection.";
            }
        }
        catch (Exception ex)
        {
            UnsupportedReason = $"Failed to initialize X11/XTest libraries: {ex.Message}";
        }
    }

    public void MoveRelative(int dx, int dy)
    {
        if (!IsSupported) return;

        try
        {
            if (XQueryPointer(_display, _rootWindow, out _, out _, out int rootX, out int rootY, out _, out _, out _))
            {
                MoveAbsolute(rootX + dx, rootY + dy);
            }
        }
        catch { }
    }

    public void MoveAbsolute(int x, int y)
    {
        if (!IsSupported) return;

        try
        {
            XTestFakeMotionEvent(_display, -1, x, y, 0);
            XFlush(_display);
        }
        catch { }
    }

    public void SetButton(MouseButton button, bool isDown)
    {
        if (!IsSupported) return;

        uint xBtn = button switch
        {
            MouseButton.Left => 1,
            MouseButton.Middle => 2,
            MouseButton.Right => 3,
            _ => 1
        };

        try
        {
            XTestFakeButtonEvent(_display, xBtn, isDown, 0);
            XFlush(_display);
        }
        catch { }
    }

    public void Scroll(int horizontal, int vertical)
    {
        if (!IsSupported) return;

        try
        {
            // Vertical scroll: 120 units per notch; 4 = Up, 5 = Down
            int vNotches = vertical / 120;
            if (vNotches == 0 && vertical != 0) vNotches = vertical > 0 ? 1 : -1;

            uint vButton = vNotches > 0 ? 4u : 5u;
            for (int i = 0; i < Math.Abs(vNotches); i++)
            {
                XTestFakeButtonEvent(_display, vButton, true, 0);
                XTestFakeButtonEvent(_display, vButton, false, 0);
            }

            // Horizontal scroll: 120 units per notch; 6 = Left, 7 = Right
            int hNotches = horizontal / 120;
            if (hNotches == 0 && horizontal != 0) hNotches = horizontal > 0 ? 1 : -1;

            uint hButton = hNotches > 0 ? 7u : 6u;
            for (int i = 0; i < Math.Abs(hNotches); i++)
            {
                XTestFakeButtonEvent(_display, hButton, true, 0);
                XTestFakeButtonEvent(_display, hButton, false, 0);
            }

            XFlush(_display);
        }
        catch { }
    }

    public void Key(ushort virtualKey, bool isDown)
    {
        if (!IsSupported) return;

        if (!X11KeyMap.TryGetKeysym(virtualKey, out uint keysym)) return;

        try
        {
            byte keycode = XKeysymToKeycode(_display, keysym);
            if (keycode != 0)
            {
                XTestFakeKeyEvent(_display, keycode, isDown, 0);
                XFlush(_display);
            }
        }
        catch { }
    }

    public void Char(ushort utf16Unit, bool isDown)
    {
        if (!IsSupported) return;

        // X11 Unicode Keysym: 0x01000000 + codepoint (for codepoints > 0xFF), or ASCII codepoint
        uint keysym = utf16Unit <= 0xFF ? utf16Unit : (0x01000000u | utf16Unit);

        try
        {
            byte keycode = XKeysymToKeycode(_display, keysym);
            if (keycode != 0)
            {
                XTestFakeKeyEvent(_display, keycode, isDown, 0);
                XFlush(_display);
            }
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_display != IntPtr.Zero)
        {
            try
            {
                XCloseDisplay(_display);
            }
            catch { }
            _display = IntPtr.Zero;
        }
    }

    // P/Invoke bindings to libX11.so.6 and libXtst.so.6
    [DllImport("libX11.so.6", EntryPoint = "XOpenDisplay")]
    private static extern IntPtr XOpenDisplay(IntPtr displayName);

    [DllImport("libX11.so.6", EntryPoint = "XCloseDisplay")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6", EntryPoint = "XDefaultRootWindow")]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6", EntryPoint = "XFlush")]
    private static extern int XFlush(IntPtr display);

    [DllImport("libX11.so.6", EntryPoint = "XKeysymToKeycode")]
    private static extern byte XKeysymToKeycode(IntPtr display, uint keysym);

    [DllImport("libX11.so.6", EntryPoint = "XQueryPointer")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool XQueryPointer(
        IntPtr display,
        IntPtr window,
        out IntPtr rootReturn,
        out IntPtr childReturn,
        out int rootXReturn,
        out int rootYReturn,
        out int winXReturn,
        out int winYReturn,
        out uint maskReturn);

    [DllImport("libXtst.so.6", EntryPoint = "XTestFakeMotionEvent")]
    private static extern int XTestFakeMotionEvent(IntPtr display, int screenNumber, int x, int y, ulong delay);

    [DllImport("libXtst.so.6", EntryPoint = "XTestFakeButtonEvent")]
    private static extern int XTestFakeButtonEvent(IntPtr display, uint button, [MarshalAs(UnmanagedType.Bool)] bool isPress, ulong delay);

    [DllImport("libXtst.so.6", EntryPoint = "XTestFakeKeyEvent")]
    private static extern int XTestFakeKeyEvent(IntPtr display, byte keycode, [MarshalAs(UnmanagedType.Bool)] bool isPress, ulong delay);
}
