using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Mouser.UI.Services;

/// <summary>
/// Probes the foreground application and window title, or the clipboard owner window.
/// </summary>
public static class ForegroundApp
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardOwner();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    public static (string App, string Title) Get()
    {
        if (!OperatingSystem.IsWindows())
        {
            return ("", "");
        }

        try
        {
            IntPtr hwnd = GetClipboardOwner();
            if (hwnd == IntPtr.Zero)
            {
                hwnd = GetForegroundWindow();
            }

            if (hwnd == IntPtr.Zero)
            {
                return ("", "");
            }

            string app = "";
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != 0)
            {
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    app = proc.ProcessName ?? "";
                }
                catch
                {
                    app = "";
                }
            }

            string title = "";
            try
            {
                var sb = new StringBuilder(201);
                int len = GetWindowText(hwnd, sb, 200);
                if (len > 0)
                {
                    title = sb.ToString();
                }
            }
            catch
            {
                title = "";
            }

            return (app, title);
        }
        catch
        {
            return ("", "");
        }
    }
}
