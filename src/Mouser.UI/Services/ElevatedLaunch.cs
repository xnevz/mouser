using System;
using System.Diagnostics;

namespace Mouser.UI.Services;

public static class ElevatedLaunch
{
    public static Action? ShutdownHandler { get; set; }

    public static bool IsElevated => ElevationInfo.IsCurrentProcessElevated();

    public static string GetCreateTaskCommand(string exePath, string args = "--minimized") =>
        $"/Create /F /TN \"Mouser\" /TR \"\\\"{exePath}\\\" {args}\" /SC ONLOGON /RL HIGHEST /IT";

    public static string GetDeleteTaskCommand() =>
        "/Delete /F /TN \"Mouser\"";

    public static string GetQueryTaskCommand() =>
        "/Query /TN \"Mouser\"";

    public static (bool Success, string Error) CreateScheduledTask(string? exePath = null, string args = "--minimized")
    {
        if (!OperatingSystem.IsWindows()) return (false, "Not supported on this OS");
        try
        {
            exePath ??= Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return (false, "Could not determine process path");

            var commandArgs = GetCreateTaskCommand(exePath, args);
            var psi = new ProcessStartInfo("schtasks.exe", commandArgs)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using var p = Process.Start(psi);
            if (p == null) return (false, "Failed to start schtasks.exe");
            p.WaitForExit(5000);
            string err = p.StandardError.ReadToEnd();
            string outStr = p.StandardOutput.ReadToEnd();
            bool success = p.ExitCode == 0;
            return (success, success ? string.Empty : (string.IsNullOrWhiteSpace(err) ? outStr : err));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static (bool Success, string Error) DeleteScheduledTask()
    {
        if (!OperatingSystem.IsWindows()) return (false, "Not supported on this OS");
        try
        {
            var commandArgs = GetDeleteTaskCommand();
            var psi = new ProcessStartInfo("schtasks.exe", commandArgs)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using var p = Process.Start(psi);
            if (p == null) return (false, "Failed to start schtasks.exe");
            p.WaitForExit(5000);
            string err = p.StandardError.ReadToEnd();
            string outStr = p.StandardOutput.ReadToEnd();
            bool success = p.ExitCode == 0;
            return (success, success ? string.Empty : (string.IsNullOrWhiteSpace(err) ? outStr : err));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static bool TaskExists()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            var commandArgs = GetQueryTaskCommand();
            var psi = new ProcessStartInfo("schtasks.exe", commandArgs)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool RestartElevated(string[]? extraArgs = null)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return false;

            var psi = new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            if (extraArgs != null)
            {
                foreach (var arg in extraArgs)
                {
                    psi.ArgumentList.Add(arg);
                }
            }
            var p = Process.Start(psi);
            if (p != null)
            {
                TriggerShutdown();
                return true;
            }
            return false;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    public static bool RestartNormal(string[]? extraArgs = null)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return false;

            var psi = new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = ""
            };
            if (extraArgs != null)
            {
                foreach (var arg in extraArgs)
                {
                    psi.ArgumentList.Add(arg);
                }
            }
            var p = Process.Start(psi);
            if (p != null)
            {
                TriggerShutdown();
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void TriggerShutdown()
    {
        if (ShutdownHandler != null)
        {
            ShutdownHandler();
            return;
        }

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                desktop.Shutdown();
            }
            else
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => desktop.Shutdown());
            }
        }
    }
}
