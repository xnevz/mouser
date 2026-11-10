using System.Runtime.InteropServices;

namespace Mouser.Core.Interop;

/// <summary>
/// Source-generated P/Invoke bindings to the native C/Objective-C++ driver (libmouser_native).
/// </summary>
public static partial class NativeDriver
{
    private const string LibraryName = "mouser_native";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MouseMoveDelegate(short dx, short dy);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MouseButtonDelegate(byte button, [MarshalAs(UnmanagedType.Bool)] bool isDown);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void KeyDelegate(uint keyCode, byte modifiers, [MarshalAs(UnmanagedType.Bool)] bool isDown);

    [LibraryImport(LibraryName, EntryPoint = "mouser_get_native_version")]
    public static partial uint GetNativeVersion();

    [LibraryImport(LibraryName, EntryPoint = "mouser_native_ping")]
    public static partial int NativePing(int value);

    [LibraryImport(LibraryName, EntryPoint = "mouser_check_accessibility_permission")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CheckAccessibilityPermission();

    [LibraryImport(LibraryName, EntryPoint = "mouser_init_input_capture")]
    public static partial int InitInputCapture(
        MouseMoveDelegate onMove,
        MouseButtonDelegate onButton,
        KeyDelegate onKey);

    [LibraryImport(LibraryName, EntryPoint = "mouser_stop_input_capture")]
    public static partial void StopInputCapture();

    [LibraryImport(LibraryName, EntryPoint = "mouser_inject_mouse_move")]
    public static partial void InjectMouseMove(int targetX, int targetY);

    [LibraryImport(LibraryName, EntryPoint = "mouser_inject_mouse_delta")]
    public static partial void InjectMouseDelta(short dx, short dy);

    [LibraryImport(LibraryName, EntryPoint = "mouser_inject_mouse_button")]
    public static partial void InjectMouseButton(byte button, [MarshalAs(UnmanagedType.Bool)] bool isDown);

    [LibraryImport(LibraryName, EntryPoint = "mouser_inject_key")]
    public static partial void InjectKey(uint keyCode, byte modifiers, [MarshalAs(UnmanagedType.Bool)] bool isDown);

    [LibraryImport(LibraryName, EntryPoint = "mouser_set_input_redirection")]
    public static partial void SetInputRedirection([MarshalAs(UnmanagedType.Bool)] bool enabled);

    [LibraryImport(LibraryName, EntryPoint = "mouser_is_input_redirected")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsInputRedirected();

    [LibraryImport(LibraryName, EntryPoint = "mouser_get_cursor_position")]
    public static partial void GetCursorPosition(out int x, out int y);

    [LibraryImport(LibraryName, EntryPoint = "mouser_set_cursor_position")]
    public static partial void SetCursorPosition(int x, int y);
}
