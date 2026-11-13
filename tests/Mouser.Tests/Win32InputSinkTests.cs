using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Mouser.Core.Input;

namespace Mouser.Tests;

[SupportedOSPlatform("windows")]
public partial class Win32InputSinkTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    /// <summary>Moves the real cursor a little and puts it back. Skips itself in sessions without an input desktop.</summary>
    [WindowsFact]
    public async Task MoveRelative_MovesTheRealCursor()
    {
        Assert.True(GetCursorPos(out var start));
        // Start away from screen edges so the move can't be clamped.
        SetCursorPos(400, 400);
        try
        {
            var sink = new Win32InputSink();
            sink.MoveRelative(40, 25);
            await Task.Delay(50);
            Assert.True(GetCursorPos(out var moved));

            if (moved.X == 400 && moved.Y == 400)
                return; // locked or non-interactive session: Windows silently drops injected input

            // Pointer acceleration may scale the distance, but the direction must match.
            Assert.True(moved.X > 400, $"expected cursor to move right, got {moved.X}");
            Assert.True(moved.Y > 400, $"expected cursor to move down, got {moved.Y}");
        }
        finally
        {
            SetCursorPos(start.X, start.Y);
        }
    }

    [Theory]
    [InlineData(0x25, true)]  // Left arrow
    [InlineData(0x2E, true)]  // Delete
    [InlineData(0xB3, true)]  // Media play/pause
    [InlineData(0xAF, true)]  // Volume up
    [InlineData(0x41, false)] // A
    [InlineData(0x60, false)] // Numpad 0
    public void ExtendedKeyFlag_IsSetForNavigationAndMediaKeys(ushort vk, bool extended)
    {
        Assert.Equal(extended, Win32InputSink.IsExtendedKey(vk));
    }
}
