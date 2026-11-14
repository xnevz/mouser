using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Win32;
using Mouser.Core.Clipboard;

namespace Mouser.UI.Services;

/// <summary>System clipboard access, abstracted so view models stay testable without a window.</summary>
public interface IClipboardAccess
{
    Task<string?> GetTextAsync();
    Task SetTextAsync(string text);
    Task<ClipboardSnapshot> GetSnapshotAsync();
    Task SetSnapshotAsync(ClipboardSnapshot s);
}

/*
 * Spike Findings — Avalonia 12.1.6 Clipboard & Win32 Interop (Task D4):
 *
 * 1. Avalonia 12.1.6 Platform Clipboard APIs:
 *    - `Avalonia.Input.Platform.IClipboard` exposes:
 *      * ClearAsync(), FlushAsync()
 *      * SetDataAsync(IAsyncDataTransfer dataTransfer)
 *      * TryGetDataAsync() -> Task<IAsyncDataTransfer?>
 *      * TryGetInProcessDataAsync() -> Task<IAsyncDataTransfer?>
 *    - Extension methods on `Avalonia.Input.Platform.ClipboardExtensions`:
 *      * TryGetTextAsync(IClipboard), SetTextAsync(IClipboard, string)
 *      * TryGetBitmapAsync(IClipboard) -> Task<Bitmap?>, SetBitmapAsync(IClipboard, Bitmap)
 *      * TryGetFilesAsync(IClipboard) -> Task<IEnumerable<IStorageItem>?>, SetFilesAsync(IClipboard, IEnumerable<IStorageItem>)
 *      * GetDataFormatsAsync(IClipboard) -> Task<IReadOnlyList<DataFormat>?>
 *      * TryGetValueAsync<T>(IClipboard, DataFormat<T>), SetValueAsync<T>(IClipboard, DataFormat<T>, T)
 *    - `Avalonia.Input.DataFormat` only contains built-in static properties for:
 *      * DataFormat.Text (Universal: Text)
 *      * DataFormat.Bitmap (Universal: Bitmap)
 *      * DataFormat.File (Universal: File)
 *      (Old `DataFormats` class is obsolete with warning CS0619).
 *
 * 2. Gaps & Limitations in Avalonia 12.1.6:
 *    - HTML: Avalonia lacks built-in support for HTML clipboard data (`DataFormat` has no Html property).
 *    - PNG Extraction: Avalonia's `TryGetBitmapAsync` returns an `Avalonia.Media.Imaging.Bitmap`.
 *      Extracting PNG bytes requires `bitmap.Save(stream)` which requires Avalonia's rendering subsystem
 *      (Skia) to be active. In headless or unit-test environments without `IPlatformRenderInterface`,
 *      creating or saving Bitmaps throws `InvalidOperationException`.
 *    - Files: `SetFilesAsync` accepts `IStorageItem` items rather than plain file paths, which cannot
 *      be easily created from plain path strings without an active `IStorageProvider`.
 *
 * 3. Windows Win32 P/Invoke Solution (Win32Clipboard):
 *    - Windows clipboard formats handled directly via Win32:
 *      * "HTML Format": Registered via RegisterClipboardFormatW("HTML Format"). Read as UTF-8 string;
 *        written with standard CF_HTML header (Version:0.9, StartHTML, EndHTML, StartFragment, EndFragment).
 *      * "PNG": Registered via RegisterClipboardFormatW("PNG"). Raw PNG bytes can be read/written directly
 *        without any image re-encoding or Skia dependency.
 *      * CF_HDROP (15): Read using shell32!DragQueryFileW; written using standard DROPFILES struct.
 *      * CF_DIB (8) / CF_DIBV5 (17): Present when images are copied via Snipping Tool or Paint.
 *        Decoded by Avalonia's `TryGetBitmapAsync` into `Bitmap`, then exported to PNG via `bitmap.Save(ms)`.
 *      * CF_UNICODETEXT (13): Standard UTF-16 text read/write.
 *    - Concurrency/Contention: Win32 clipboard operations retry up to 5 times with 20 ms delay when
 *      other apps momentarily lock the clipboard.
 */
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

    public async Task<ClipboardSnapshot> GetSnapshotAsync()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var snapshot = Win32Clipboard.GetSnapshot();

                // If PNG was not directly available on Windows clipboard, check if CF_DIB / CF_DIBV5
                // is present and decode via Avalonia's TryGetBitmapAsync.
                if (snapshot.ImagePng == null && Win32Clipboard.HasDib())
                {
                    try
                    {
                        var bitmap = await Avalonia.Input.Platform.ClipboardExtensions.TryGetBitmapAsync(_clipboard);
                        if (bitmap != null)
                        {
                            using var ms = new MemoryStream();
                            bitmap.Save(ms, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                            snapshot = snapshot with { ImagePng = ms.ToArray() };
                        }
                    }
                    catch
                    {
                        // Ignore bitmap conversion failures (e.g. headless without renderer)
                    }
                }

                // Fallback for text if Win32 read was null
                if (snapshot.Text == null)
                {
                    string? avaloniaText = await GetTextAsync();
                    if (avaloniaText != null)
                        snapshot = snapshot with { Text = avaloniaText };
                }

                return snapshot;
            }

            // Cross-platform fallback for non-Windows (macOS / Linux)
            string? text = await GetTextAsync();
            byte[]? png = null;
            try
            {
                var bitmap = await Avalonia.Input.Platform.ClipboardExtensions.TryGetBitmapAsync(_clipboard);
                if (bitmap != null)
                {
                    using var ms = new MemoryStream();
                    bitmap.Save(ms, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                    png = ms.ToArray();
                }
            }
            catch { }

            List<string>? files = null;
            try
            {
                var storageItems = await Avalonia.Input.Platform.ClipboardExtensions.TryGetFilesAsync(_clipboard);
                if (storageItems != null)
                {
                    files = new List<string>();
                    foreach (var item in storageItems)
                    {
                        if (item.Path.IsFile)
                            files.Add(item.Path.LocalPath);
                    }
                }
            }
            catch { }

            return new ClipboardSnapshot(text, null, png, files);
        }
        catch
        {
            return new ClipboardSnapshot(null, null, null, null);
        }
    }

    public async Task SetSnapshotAsync(ClipboardSnapshot s)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // When an image is set without HTML, use Avalonia to set CF_DIBV5 / CF_BITMAP so standard apps can paste it
                if (s.ImagePng is { Length: > 0 } && string.IsNullOrEmpty(s.Html))
                {
                    try
                    {
                        using var ms = new MemoryStream(s.ImagePng);
                        var bitmap = new Avalonia.Media.Imaging.Bitmap(ms);
                        await Avalonia.Input.Platform.ClipboardExtensions.SetBitmapAsync(_clipboard, bitmap);
                        return;
                    }
                    catch
                    {
                        // Fall back to Win32 raw PNG
                    }
                }

                Win32Clipboard.SetSnapshot(s);
                return;
            }

            // Cross-platform
            if (s.ImagePng is { Length: > 0 })
            {
                using var ms = new MemoryStream(s.ImagePng);
                var bitmap = new Avalonia.Media.Imaging.Bitmap(ms);
                await Avalonia.Input.Platform.ClipboardExtensions.SetBitmapAsync(_clipboard, bitmap);
            }
            else if (s.Text != null)
            {
                await SetTextAsync(s.Text);
            }
            else if (s.IsEmpty)
            {
                await _clipboard.ClearAsync();
            }
        }
        catch
        {
            // Clipboard access error
        }
    }
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
            key.SetValue(ValueName, $"\"{exe}\" --minimized");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
