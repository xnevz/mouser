using System;
using System.Runtime.InteropServices;

namespace Mouser.UI.Services;

public static class WindowsSession
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr desktop);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    /// <summary>True while the lock screen (secure desktop) is showing.</summary>
    public static bool IsWorkstationLocked()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var h = OpenInputDesktop(0, false, 0x0100 /*DESKTOP_SWITCHDESKTOP*/);
        if (h == IntPtr.Zero) return true;
        CloseDesktop(h);
        return false;
    }

    public static TimeSpan IdleTime()
    {
        if (!OperatingSystem.IsWindows()) return TimeSpan.Zero;
        var i = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return GetLastInputInfo(ref i) ? TimeSpan.FromMilliseconds((uint)Environment.TickCount - i.dwTime) : TimeSpan.Zero;
    }
}
