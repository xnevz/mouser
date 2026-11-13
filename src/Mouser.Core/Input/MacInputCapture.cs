using System.Runtime.Versioning;
using Mouser.Core.Interop;
using Mouser.Core.Kvm;

namespace Mouser.Core.Input;

/// <summary>
/// macOS capture through the CGEventTap in libmouser_native. Needs the Accessibility permission.
/// shortcut: untested on real hardware, and the native tap reports no wheel events, so scrolling isn't forwarded
/// from a Mac yet; verify on a Mac and add a scroll callback to platform_mac.mm.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacInputCapture : IInputCapture
{
    private readonly KeyTranslationService _keys = new();
    // Kept in fields: the native side holds raw function pointers to these delegates.
    private readonly NativeDriver.MouseMoveDelegate _onMove;
    private readonly NativeDriver.MouseButtonDelegate _onButton;
    private readonly NativeDriver.KeyDelegate _onKey;
    private bool _running;

    public bool IsSupported => true;
    public Func<CapturedInput, bool>? Handler { get; set; }

    public MacInputCapture()
    {
        _onMove = OnMove;
        _onButton = OnButton;
        _onKey = OnKey;
    }

    public void Start()
    {
        if (_running) return;
        int result = NativeDriver.InitInputCapture(_onMove, _onButton, _onKey);
        if (result == -1) throw new InvalidOperationException("Allow Mouser in System Settings → Privacy & Security → Accessibility, then restart it.");
        if (result != 0) throw new InvalidOperationException($"Couldn't create the macOS event tap ({result}).");
        _running = true;
    }

    public void Stop()
    {
        if (!_running) return;
        NativeDriver.SetInputRedirection(false);
        NativeDriver.StopInputCapture();
        _running = false;
    }

    public void WarpCursor(int x, int y) => NativeDriver.SetCursorPosition(x, y);

    private void OnMove(short dx, short dy)
    {
        NativeDriver.GetCursorPosition(out int x, out int y);
        Dispatch(new CapturedInput(CaptureKind.Move, x + dx, y + dy, dx, dy));
    }

    private void OnButton(byte button, bool isDown) =>
        Dispatch(new CapturedInput(CaptureKind.Button, Button: button switch { 1 => MouseButton.Left, 2 => MouseButton.Right, _ => MouseButton.Middle }, IsDown: isDown));

    private void OnKey(uint macKey, byte modifiers, bool isDown) =>
        Dispatch(new CapturedInput(CaptureKind.Key, Key: (ushort)_keys.TranslateMacToWindows(macKey), IsDown: isDown));

    /// <summary>
    /// The native tap swallows events while its redirect flag is set, and decides before calling us, so the flag
    /// follows the controller's answer one event late. That costs one event at each switch, never a stuck input.
    /// </summary>
    private void Dispatch(CapturedInput input)
    {
        bool swallow;
        try
        {
            swallow = Handler?.Invoke(input) ?? false;
        }
        catch
        {
            swallow = false; // never leave the Mac's input swallowed because of a bug
        }
        NativeDriver.SetInputRedirection(swallow);
    }

    public void Dispose() => Stop();
}
