using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Mouser.UI.Services;

/// <summary>
/// Applies Win32 extended styles to make overlay windows transparent to mouse clicks and touch,
/// and prevent them from appearing in the taskbar/Alt-Tab or taking input focus.
/// </summary>
public static class ClickThrough
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TRANSPARENT = 0x00000020L;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const long WS_EX_LAYERED = 0x00080000L;
    private const long WS_EX_NOACTIVATE = 0x08000000L;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static long GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return GetWindowLongPtr64(hWnd, nIndex).ToInt64();
        return GetWindowLong32(hWnd, nIndex);
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    /// <summary>
    /// Configures the window to pass all mouse input through to windows underneath.
    /// Safely checks for Windows OS and valid HWND handle; safely no-ops on other platforms or invalid handles.
    /// </summary>
    public static void Apply(Window? window)
    {
        if (!OperatingSystem.IsWindows() || window == null) return;

        try
        {
            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            Apply(handle);
        }
        catch
        {
            // Safely ignore platform interop failures
        }
    }

    /// <summary>
    /// Configures the window handle to pass all mouse input through to windows underneath.
    /// Safely checks for Windows OS and non-zero handle.
    /// </summary>
    public static void Apply(IntPtr handle)
    {
        if (!OperatingSystem.IsWindows() || handle == IntPtr.Zero) return;

        try
        {
            long flags = WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            long current = GetWindowLongPtr(handle, GWL_EXSTYLE);
            SetWindowLongPtr(handle, GWL_EXSTYLE, new IntPtr(current | flags));
        }
        catch
        {
            // Safely ignore platform interop failures
        }
    }
}
