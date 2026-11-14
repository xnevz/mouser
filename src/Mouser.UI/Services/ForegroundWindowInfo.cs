using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Mouser.UI.Services;

/// <summary>
/// Probes the state of the Windows foreground window (fullscreen or maximized).
/// Used by SwitchPolicy to prevent switching when full-screen apps or games are active.
/// </summary>
public static class ForegroundWindowInfo
{
    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint TOKEN_QUERY = 0x0008;
    private const int TokenElevation = 20;
    private const int ERROR_ACCESS_DENIED = 5;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, out int TokenInformation, int TokenInformationLength, out int ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private static long GetWindowStyle(IntPtr hWnd)
    {
        if (IntPtr.Size == 8)
            return GetWindowLongPtr64(hWnd, GWL_STYLE).ToInt64();
        return GetWindowLong32(hWnd, GWL_STYLE);
    }

    public static (bool Fullscreen, bool Maximized) Get()
    {
        if (!OperatingSystem.IsWindows())
        {
            return (false, false);
        }

        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return (false, false);
            }

            var sb = new StringBuilder(256);
            if (GetClassName(hwnd, sb, sb.Capacity) > 0)
            {
                string className = sb.ToString();
                if (className is "Progman" or "WorkerW" or "Shell_TrayWnd")
                {
                    return (false, false);
                }
            }

            bool isMaximized = IsZoomed(hwnd);
            bool isFullscreen = false;

            if (GetWindowRect(hwnd, out var windowRect))
            {
                IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (hMonitor != IntPtr.Zero)
                {
                    var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                    if (GetMonitorInfo(hMonitor, ref mi))
                    {
                        bool matchesMonitor = windowRect.Left == mi.rcMonitor.Left &&
                                              windowRect.Top == mi.rcMonitor.Top &&
                                              windowRect.Right == mi.rcMonitor.Right &&
                                              windowRect.Bottom == mi.rcMonitor.Bottom;
                        long style = GetWindowStyle(hwnd);
                        bool noCaption = (style & WS_CAPTION) == 0;
                        isFullscreen = matchesMonitor && noCaption;
                    }
                }
            }

            return (isFullscreen, isMaximized);
        }
        catch
        {
            return (false, false);
        }
    }

    /// <summary>
    /// Checks whether the foreground window process is elevated when Mouser is NOT elevated.
    /// Returns false if Mouser is already elevated (since Mouser can type into it), or on non-Windows.
    /// </summary>
    public static bool IsElevated()
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (ElevationInfo.IsCurrentProcessElevated()) return false;

        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0 || pid == (uint)Environment.ProcessId) return false;

            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                if (err == ERROR_ACCESS_DENIED)
                {
                    // Typically elevated or SYSTEM process protected from non-elevated callers
                    return true;
                }
                return false;
            }

            try
            {
                if (OpenProcessToken(hProcess, TOKEN_QUERY, out IntPtr hToken))
                {
                    try
                    {
                        int size = sizeof(int);
                        if (GetTokenInformation(hToken, TokenElevation, out int isElevated, size, out _))
                        {
                            return isElevated != 0;
                        }
                    }
                    finally
                    {
                        CloseHandle(hToken);
                    }
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
