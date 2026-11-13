using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Mouser.Core.Input;

namespace Mouser.UI.Services;

/// <summary>
/// System-wide Win+Alt+V hotkey for the quick-paste overlay, plus the focus and Ctrl+V plumbing needed to
/// paste into the app that was in front. Windows only.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class QuickPasteHotkey : IDisposable
{
    public const string Description = "Win+Alt+V";

    private const uint MOD_ALT = 0x1, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;
    private const uint VK_V = 0x56;
    private const uint WM_HOTKEY = 0x0312, WM_QUIT = 0x0012;

    private readonly Thread _thread;
    private uint _threadId;

    /// <summary>False when another app already owns the hotkey.</summary>
    public bool IsRegistered { get; private set; }

    /// <summary>Raised on the hotkey thread with the window that was in front when the hotkey was pressed.</summary>
    public event Action<nint>? Pressed;

    public QuickPasteHotkey()
    {
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            IsRegistered = RegisterHotKey(0, 1, MOD_WIN | MOD_ALT | MOD_NOREPEAT, VK_V);
            ready.Set();
            while (GetMessageW(out var msg, 0, 0, 0) > 0)
            {
                if (msg.message == WM_HOTKEY) Pressed?.Invoke(GetForegroundWindow());
            }
            if (IsRegistered) UnregisterHotKey(0, 1);
        })
        { IsBackground = true, Name = "Mouser quick-paste hotkey" };
        _thread.Start();
        ready.Wait();
    }

    /// <summary>Gives focus back to <paramref name="window"/> and presses Ctrl+V there.</summary>
    public static void PasteInto(nint window, IInputSink keys)
    {
        if (window != 0) SetForegroundWindow(window);
        Thread.Sleep(60); // let the target window actually take focus before the keystroke arrives
        keys.Key(0x11, true);  // Ctrl
        keys.Key(0x56, true);  // V
        keys.Key(0x56, false);
        keys.Key(0x11, false);
    }

    public void Dispose()
    {
        PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
        _thread.Join(TimeSpan.FromSeconds(2));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public int x, y; }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hWnd, int id);
    [DllImport("user32.dll")] private static extern int GetMessageW(out MSG msg, nint hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PostThreadMessageW(uint thread, uint msg, nint w, nint l);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hWnd);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
