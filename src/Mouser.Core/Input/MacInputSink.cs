using System.Runtime.Versioning;
using Mouser.Core.Interop;
using Mouser.Core.Kvm;

namespace Mouser.Core.Input;

/// <summary>
/// macOS injection through libmouser_native (CGEvent). Requires the Accessibility permission.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacInputSink : IInputSink
{
    private readonly KeyTranslationService _keys = new();
    private readonly bool _available;

    public MacInputSink()
    {
        try
        {
            NativeDriver.GetNativeVersion();
            _available = true;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            _available = false;
        }
    }

    public bool IsSupported => _available;

    public void MoveRelative(int dx, int dy)
    {
        if (!_available) return;
        NativeDriver.InjectMouseDelta(ClampShort(dx), ClampShort(dy));
    }

    public void SetButton(MouseButton button, bool isDown)
    {
        if (!_available) return;
        byte native = button switch { MouseButton.Left => 1, MouseButton.Right => 2, _ => 3 };
        NativeDriver.InjectMouseButton(native, isDown);
    }

    public void Scroll(int horizontal, int vertical)
    {
        if (!_available) return;
        NativeDriver.InjectScroll(horizontal, vertical);
    }

    public void Key(ushort virtualKey, bool isDown)
    {
        // shortcut: unmapped keys (incl. media keys, which need NSSystemDefined events) are dropped on macOS.
        if (!_available || !_keys.TryTranslateWindowsToMac(virtualKey, out uint macKey)) return;
        NativeDriver.InjectKey(macKey, 0, isDown);
    }

    private static short ClampShort(int v) => (short)Math.Clamp(v, short.MinValue, short.MaxValue);
}
