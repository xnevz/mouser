using System;
using System.Runtime.InteropServices;

namespace Mouser.UI.Services;

public static class WindowsPower
{
    public static Action? LockHook { get; set; }
    public static Action? DisplayOffHook { get; set; }
    public static Action? SleepHook { get; set; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    private const uint WM_SYSCOMMAND = 0x0112;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);
    private static readonly IntPtr SC_MONITORPOWER = new(0xF170);
    private static readonly IntPtr MONITOR_OFF = new(2);

    public static void Lock()
    {
        if (LockHook != null)
        {
            LockHook.Invoke();
            return;
        }

        if (!OperatingSystem.IsWindows()) return;
        try
        {
            LockWorkStation();
        }
        catch
        {
            // Ignore power invoke errors
        }
    }

    public static void TurnOffDisplay()
    {
        if (DisplayOffHook != null)
        {
            DisplayOffHook.Invoke();
            return;
        }

        if (!OperatingSystem.IsWindows()) return;
        try
        {
            SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_OFF);
        }
        catch
        {
            // Ignore power invoke errors
        }
    }

    public static void Sleep()
    {
        if (SleepHook != null)
        {
            SleepHook.Invoke();
            return;
        }

        if (!OperatingSystem.IsWindows()) return;
        try
        {
            SetSuspendState(false, true, false);
        }
        catch
        {
            // Ignore power invoke errors
        }
    }
}
