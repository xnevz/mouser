using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Mouser.Core.Input;

/// <summary>
/// Global mouse/keyboard capture through WH_MOUSE_LL / WH_KEYBOARD_LL hooks on a dedicated message-loop thread.
/// Injected events (from SendInput, including Mouser's own injection) are ignored so they can't loop back.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32InputCapture : IInputCapture
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const uint WM_QUIT = 0x0012;
    private const uint LLMHF_INJECTED = 0x01;
    private const uint LLKHF_INJECTED = 0x10;

    private readonly HookProc _mouseProc;
    private readonly HookProc _keyboardProc;
    private Thread? _thread;
    private uint _threadId;
    private nint _mouseHook;
    private nint _keyboardHook;

    public bool IsSupported => true;
    public Func<CapturedInput, bool>? Handler { get; set; }

    public Win32InputCapture()
    {
        // Delegates live in fields so the GC can't collect them while Windows holds the function pointers.
        _mouseProc = MouseHook;
        _keyboardProc = KeyboardHook;
    }

    public void Start()
    {
        if (_thread != null) return;

        using var ready = new ManualResetEventSlim();
        string? error = null;
        _thread = new Thread(() =>
        {
            nint module = GetModuleHandleW(null);
            _mouseHook = SetWindowsHookExW(WH_MOUSE_LL, _mouseProc, module, 0);
            _keyboardHook = SetWindowsHookExW(WH_KEYBOARD_LL, _keyboardProc, module, 0);
            _threadId = GetCurrentThreadId();
            if (_mouseHook == 0 || _keyboardHook == 0) error = $"SetWindowsHookEx failed ({Marshal.GetLastPInvokeError()})";
            ready.Set();

            while (GetMessageW(out var msg, 0, 0, 0) > 0)
            {
                TranslateMessage(in msg);
                DispatchMessageW(in msg);
            }

            if (_mouseHook != 0) UnhookWindowsHookEx(_mouseHook);
            if (_keyboardHook != 0) UnhookWindowsHookEx(_keyboardHook);
            _mouseHook = _keyboardHook = 0;
        })
        {
            IsBackground = true,
            Name = "Mouser input capture",
            Priority = ThreadPriority.Highest
        };
        _thread.Start();
        ready.Wait();

        if (error != null)
        {
            Stop();
            throw new InvalidOperationException(error);
        }
    }

    public void Stop()
    {
        if (_thread == null) return;
        PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
        _thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    public void WarpCursor(int x, int y) => SetCursorPos(x, y);

    private unsafe nint MouseHook(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && Handler is { } handler)
        {
            var info = *(MSLLHOOKSTRUCT*)lParam;
            if ((info.flags & LLMHF_INJECTED) == 0 && ToCaptured((uint)wParam, info) is { } input && SafeInvoke(handler, input))
                return 1;
        }
        return CallNextHookEx(0, code, wParam, lParam);
    }

    private unsafe nint KeyboardHook(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && Handler is { } handler)
        {
            var info = *(KBDLLHOOKSTRUCT*)lParam;
            if ((info.flags & LLKHF_INJECTED) == 0)
            {
                uint msg = (uint)wParam;
                bool isDown = msg is 0x0100 or 0x0104; // WM_KEYDOWN, WM_SYSKEYDOWN
                if (SafeInvoke(handler, new CapturedInput(CaptureKind.Key, Key: (ushort)info.vkCode, IsDown: isDown)))
                    return 1;
            }
        }
        return CallNextHookEx(0, code, wParam, lParam);
    }

    private static CapturedInput? ToCaptured(uint msg, MSLLHOOKSTRUCT info)
    {
        switch (msg)
        {
            case 0x0200: // WM_MOUSEMOVE: the position is not clamped to the screen, so the delta also works at edges.
                GetCursorPos(out var cur);
                return new CapturedInput(CaptureKind.Move, info.x, info.y, info.x - cur.X, info.y - cur.Y);
            case 0x0201: return Button(MouseButton.Left, true);
            case 0x0202: return Button(MouseButton.Left, false);
            case 0x0204: return Button(MouseButton.Right, true);
            case 0x0205: return Button(MouseButton.Right, false);
            case 0x0207: return Button(MouseButton.Middle, true);
            case 0x0208: return Button(MouseButton.Middle, false);
            case 0x020A: return new CapturedInput(CaptureKind.Wheel, Dy: (short)(info.mouseData >> 16));
            case 0x020E: return new CapturedInput(CaptureKind.Wheel, Dx: (short)(info.mouseData >> 16));
            default: return null; // X buttons pass through untouched
        }

        static CapturedInput Button(MouseButton b, bool down) => new(CaptureKind.Button, Button: b, IsDown: down);
    }

    private static bool SafeInvoke(Func<CapturedInput, bool> handler, CapturedInput input)
    {
        try
        {
            return handler(input);
        }
        catch
        {
            // An exception escaping a hook callback would take the process down; never swallow input on failure.
            return false;
        }
    }

    public void Dispose() => Stop();

    private delegate nint HookProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public POINT pt; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public int x, y; public uint mouseData, flags, time; public nint dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public nint dwExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookExW(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out MSG lpMsg, nint hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(in MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessageW(in MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessageW(uint idThread, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? name);
}
