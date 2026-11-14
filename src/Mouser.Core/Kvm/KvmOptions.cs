using Mouser.Core.Input;

namespace Mouser.Core.Kvm;

public sealed class KvmOptions
{
    public int EdgeResistanceMs { get; init; } = 80;
    public double EdgePushPixels { get; init; } = 15;

    public bool BlockInFullscreen { get; init; } = true;
    public bool BlockInMaximized { get; init; } = false;
    public bool BlockInCorners { get; init; } = false;
    public string RequireModifier { get; init; } = "None"; // "None", "Ctrl", "Alt", "Shift"
    public bool DoubleTapOverride { get; init; } = true;

    public HotkeyBinding ReturnControlHotkey { get; init; } = HotkeyBinding.TryParse("Ctrl+Alt+Esc", out var b) ? b : default;
    public HotkeyBinding SecondaryReturnControlHotkey { get; init; } = HotkeyBinding.TryParse("Scroll Lock", out var b) ? b : default;
    public HotkeyBinding LockCursorHotkey { get; init; } = default;
    public bool PanicDoublePressCtrl { get; init; } = true;

    public IReadOnlyList<HotkeyBinding> JumpHotkeys { get; init; } = Array.Empty<HotkeyBinding>();
    public HotkeyBinding NextComputerHotkey { get; init; } = HotkeyBinding.TryParse("Ctrl+Alt+Right", out var b) ? b : default;

    public static KvmOptions FromFeel(string? feel) => feel switch
    {
        "Relaxed" => new() { EdgeResistanceMs = 0, EdgePushPixels = 1 },
        "Careful" => new() { EdgeResistanceMs = 300, EdgePushPixels = 60 },
        _ => new() // "Normal"
    };
}
