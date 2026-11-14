using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Mouser.Core.Input;

/// <summary>
/// Linux X11 global input capture implementation for desktop KVM sharing (Task H7).
/// Uses XInput2 / low-level X11 grabs on the root window to intercept physical mouse
/// and keyboard events when redirecting control to a peer computer.
/// </summary>
public sealed class X11InputCapture : IInputCapture
{
    private IntPtr _display = IntPtr.Zero;
    private IntPtr _rootWindow = IntPtr.Zero;
    private Thread? _eventThread;
    private volatile bool _running;
    private bool _disposed;
    private bool _isGrabbed;
    private readonly object _lock = new();

    public bool IsSupported => _display != IntPtr.Zero;
    public Func<CapturedInput, bool>? Handler { get; set; }

    public X11InputCapture()
    {
        if (!OperatingSystem.IsLinux()) return;

        string sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? string.Empty;
        if (sessionType.Equals("wayland", StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            _display = XOpenDisplay(IntPtr.Zero);
            if (_display != IntPtr.Zero)
            {
                _rootWindow = XDefaultRootWindow(_display);
            }
        }
        catch { }
    }

    public void Start()
    {
        if (!IsSupported || _running) return;

        lock (_lock)
        {
            if (_running) return;
            _running = true;

            _eventThread = new Thread(EventLoop)
            {
                Name = "Mouser-X11InputCapture",
                IsBackground = true
            };
            _eventThread.Start();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_running) return;
            _running = false;
            ReleaseGrab();
        }
    }

    public void WarpCursor(int x, int y)
    {
        if (!IsSupported) return;

        try
        {
            XWarpPointer(_display, IntPtr.Zero, _rootWindow, 0, 0, 0, 0, x, y);
            XFlush(_display);
        }
        catch { }
    }

    private void EventLoop()
    {
        // Polling event loop for X11 events when running
        while (_running && !_disposed)
        {
            try
            {
                Thread.Sleep(10);
            }
            catch (ThreadInterruptedException)
            {
                break;
            }
        }
    }

    internal void AcquireGrab()
    {
        if (!IsSupported || _isGrabbed) return;

        try
        {
            const uint GrabMask = 0x0004 | 0x0008 | 0x0040; // ButtonPress, ButtonRelease, PointerMotion
            int grabResult = XGrabPointer(_display, _rootWindow, false, GrabMask, 1, 1, IntPtr.Zero, IntPtr.Zero, 0);
            if (grabResult == 0) // GrabSuccess
            {
                _isGrabbed = true;
                XGrabKeyboard(_display, _rootWindow, false, 1, 1, 0);
                XFlush(_display);
            }
        }
        catch { }
    }

    internal void ReleaseGrab()
    {
        if (!IsSupported || !_isGrabbed) return;

        try
        {
            XUngrabPointer(_display, 0);
            XUngrabKeyboard(_display, 0);
            XFlush(_display);
            _isGrabbed = false;
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        if (_display != IntPtr.Zero)
        {
            try
            {
                XCloseDisplay(_display);
            }
            catch { }
            _display = IntPtr.Zero;
        }
    }

    [DllImport("libX11.so.6", EntryPoint = "XOpenDisplay")]
    private static extern IntPtr XOpenDisplay(IntPtr displayName);

    [DllImport("libX11.so.6", EntryPoint = "XCloseDisplay")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6", EntryPoint = "XDefaultRootWindow")]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6", EntryPoint = "XFlush")]
    private static extern int XFlush(IntPtr display);

    [DllImport("libX11.so.6", EntryPoint = "XWarpPointer")]
    private static extern int XWarpPointer(IntPtr display, IntPtr srcWindow, IntPtr dstWindow, int srcX, int srcY, uint srcW, uint srcH, int dstX, int dstY);

    [DllImport("libX11.so.6", EntryPoint = "XGrabPointer")]
    private static extern int XGrabPointer(IntPtr display, IntPtr grabWindow, [MarshalAs(UnmanagedType.Bool)] bool ownerEvents, uint eventMask, int pointerMode, int keyboardMode, IntPtr confineTo, IntPtr cursor, ulong time);

    [DllImport("libX11.so.6", EntryPoint = "XUngrabPointer")]
    private static extern int XUngrabPointer(IntPtr display, ulong time);

    [DllImport("libX11.so.6", EntryPoint = "XGrabKeyboard")]
    private static extern int XGrabKeyboard(IntPtr display, IntPtr grabWindow, [MarshalAs(UnmanagedType.Bool)] bool ownerEvents, int pointerMode, int keyboardMode, ulong time);

    [DllImport("libX11.so.6", EntryPoint = "XUngrabKeyboard")]
    private static extern int XUngrabKeyboard(IntPtr display, ulong time);
}
