using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mouser.UI.Services;

/// <summary>
/// Native chrome for borderless popups (quick paste, toasts). On Windows 11 the compositor rounds the
/// window's corners and draws its hairline border and shadow, so the window can stay opaque: opaque
/// surfaces keep subpixel text antialiasing, which a transparent window with a hand-drawn rounded
/// card would lose. Elsewhere this is a no-op and the window keeps square corners.
/// </summary>
public static class WindowChrome
{
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWCP_ROUND = 2;

    public static void ApplyRoundedPopup(Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        if (window.TryGetPlatformHandle() is not { } handle || handle.Handle == IntPtr.Zero) return;

        try
        {
            int corner = DWMWCP_ROUND;
            DwmSetWindowAttribute(handle.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

            if (window.TryFindResource("PopupBorderColor", window.ActualThemeVariant, out var res) && res is Color c)
            {
                int colorRef = c.R | (c.G << 8) | (c.B << 16); // COLORREF is 0x00BBGGRR
                DwmSetWindowAttribute(handle.Handle, DWMWA_BORDER_COLOR, ref colorRef, sizeof(int));
            }
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
