namespace Mouser.Core.Input;

public enum CaptureKind { Move, Button, Wheel, Key }

/// <summary>
/// One physical input event. For moves, X/Y is where the cursor would go and Dx/Dy the movement
/// relative to the current cursor position (valid even when the cursor is parked or at a screen edge).
/// For wheels, Dx/Dy are horizontal/vertical deltas in WHEEL_DELTA units. Key is a Windows virtual-key code.
/// </summary>
public readonly record struct CapturedInput(
    CaptureKind Kind,
    int X = 0,
    int Y = 0,
    int Dx = 0,
    int Dy = 0,
    MouseButton Button = 0,
    ushort Key = 0,
    bool IsDown = false);

/// <summary>Global capture of the local mouse and keyboard, with the ability to swallow events.</summary>
public interface IInputCapture : IDisposable
{
    bool IsSupported { get; }

    /// <summary>
    /// Called for every physical (non-injected) event on the capture thread. Return true to swallow the event
    /// so the local OS never sees it. Must return quickly: Windows drops hooks that block for about a second.
    /// </summary>
    Func<CapturedInput, bool>? Handler { get; set; }

    void Start();
    void Stop();
    void WarpCursor(int x, int y);
}

public static class InputCaptures
{
    // shortcut: no Linux capture yet (needs libinput or X11 XInput2), so a Linux PC can be controlled by phones only.
    public static IInputCapture? CreateForCurrentPlatform()
    {
        if (OperatingSystem.IsWindows()) return new Win32InputCapture();
        if (OperatingSystem.IsMacOS() && new MacInputSink().IsSupported) return new MacInputCapture();
        return null;
    }
}
