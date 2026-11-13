namespace Mouser.Core.Input;

public enum MouseButton : byte
{
    Left = 0x01,
    Right = 0x02,
    Middle = 0x04
}

/// <summary>
/// Injects synthetic input into the local OS. Key codes are Windows virtual-key codes (the wire format);
/// non-Windows sinks translate them.
/// </summary>
public interface IInputSink
{
    /// <summary>False when this platform has no working injection backend.</summary>
    bool IsSupported { get; }

    void MoveRelative(int dx, int dy);

    /// <summary>Puts the cursor at a position in this computer's desktop pixel coordinates.</summary>
    void MoveAbsolute(int x, int y);
    void SetButton(MouseButton button, bool isDown);

    /// <summary>Wheel deltas in WHEEL_DELTA units (120 = one notch). Positive vertical = up, positive horizontal = right.</summary>
    void Scroll(int horizontal, int vertical);

    void Key(ushort virtualKey, bool isDown);
}

public sealed class NullInputSink : IInputSink
{
    public static readonly NullInputSink Instance = new();
    public bool IsSupported => false;
    public void MoveRelative(int dx, int dy) { }
    public void MoveAbsolute(int x, int y) { }
    public void SetButton(MouseButton button, bool isDown) { }
    public void Scroll(int horizontal, int vertical) { }
    public void Key(ushort virtualKey, bool isDown) { }
}

public static class InputSinks
{
    // shortcut: Linux has no backend yet (XTest/uinput), so phones can't control Linux hosts until one is added.
    public static IInputSink CreateForCurrentPlatform()
    {
        if (OperatingSystem.IsWindows()) return new Win32InputSink();
        if (OperatingSystem.IsMacOS()) return new MacInputSink();
        return NullInputSink.Instance;
    }
}
