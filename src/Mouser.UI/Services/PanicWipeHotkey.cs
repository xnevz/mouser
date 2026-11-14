using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Mouser.Core.Clipboard;

namespace Mouser.UI.Services;

/// <summary>
/// System-wide hotkey listener for panic wiping clipboard history. Windows only.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PanicWipeHotkey : IDisposable
{
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint WM_HOTKEY = 0x0312, WM_QUIT = 0x0012;

    private readonly Thread? _thread;
    private uint _threadId;

    public bool IsRegistered { get; private set; }
    public string HotkeyText { get; }
    public uint Modifiers { get; }
    public uint VirtualKey { get; }

    public event Action? Pressed;

    public PanicWipeHotkey(string? hotkey = null)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            HotkeyText = string.Empty;
            IsRegistered = false;
            return;
        }

        HotkeyText = hotkey;
        if (HotkeyParser.TryParse(hotkey, out uint mods, out uint vk))
        {
            Modifiers = mods;
            VirtualKey = vk;

            using var ready = new ManualResetEventSlim();
            _thread = new Thread(() =>
            {
                _threadId = GetCurrentThreadId();
                IsRegistered = RegisterHotKey(0, 2, Modifiers | MOD_NOREPEAT, VirtualKey);
                ready.Set();
                while (GetMessageW(out var msg, 0, 0, 0) > 0)
                {
                    if (msg.message == WM_HOTKEY) Pressed?.Invoke();
                }
                if (IsRegistered) UnregisterHotKey(0, 2);
            })
            { IsBackground = true, Name = "Mouser panic-wipe hotkey" };
            _thread.Start();
            ready.Wait();
        }
        else
        {
            IsRegistered = false;
        }
    }

    public PanicWipeHotkey(uint modifiers, uint virtualKey, string? hotkeyText = null)
    {
        Modifiers = modifiers;
        VirtualKey = virtualKey;
        HotkeyText = hotkeyText ?? $"{modifiers}+{virtualKey}";

        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            IsRegistered = RegisterHotKey(0, 2, Modifiers | MOD_NOREPEAT, VirtualKey);
            ready.Set();
            while (GetMessageW(out var msg, 0, 0, 0) > 0)
            {
                if (msg.message == WM_HOTKEY) Pressed?.Invoke();
            }
            if (IsRegistered) UnregisterHotKey(0, 2);
        })
        { IsBackground = true, Name = "Mouser panic-wipe hotkey" };
        _thread.Start();
        ready.Wait();
    }

    public void Dispose()
    {
        if (_thread != null && _threadId != 0)
        {
            PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
            _thread.Join(TimeSpan.FromSeconds(2));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public int x, y; }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hWnd, int id);
    [DllImport("user32.dll")] private static extern int GetMessageW(out MSG msg, nint hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PostThreadMessageW(uint thread, uint msg, nint w, nint l);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
