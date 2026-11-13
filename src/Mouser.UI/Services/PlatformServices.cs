using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Mouser.UI.Services;

/// <summary>System clipboard access, abstracted so view models stay testable without a window.</summary>
public interface IClipboardAccess
{
    Task<string?> GetTextAsync();
    Task SetTextAsync(string text);
}

public sealed class AvaloniaClipboardAccess : IClipboardAccess
{
    private readonly Avalonia.Input.Platform.IClipboard _clipboard;

    public AvaloniaClipboardAccess(Avalonia.Input.Platform.IClipboard clipboard) => _clipboard = clipboard;

    public async Task<string?> GetTextAsync()
    {
        try
        {
            return await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(_clipboard);
        }
        catch (Exception)
        {
            // Another app holds the clipboard open; try again on the next poll.
            return null;
        }
    }

    public Task SetTextAsync(string text) => Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(_clipboard, text);
}

public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Mouser";

    public static bool IsSupported => OperatingSystem.IsWindows();

    // shortcut: Windows only; macOS (LaunchAgent) and Linux (XDG autostart) need their own entries.
    public static void Apply(bool enabled)
    {
        if (OperatingSystem.IsWindows()) ApplyWindows(enabled);
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled && Environment.ProcessPath is { } exe)
            key.SetValue(ValueName, $"\"{exe}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
