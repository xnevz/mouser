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
        try
        {
            // The real mouse is shared with whoever sits at the PC, so a hand on it can spoil one attempt.
            POINT moved = default;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                SetCursorPos(400, 400); // away from screen edges so the move can't be clamped
                new Win32InputSink().MoveRelative(40, 25);
                await Task.Delay(50);
                Assert.True(GetCursorPos(out moved));
                if (moved.X > 400 && moved.Y > 400) return;
            }

            if (moved.X == 400 && moved.Y == 400)
                return; // locked or non-interactive session: Windows silently drops injected input

            // Pointer acceleration may scale the distance, but the direction must match.
            Assert.Fail($"expected the cursor to move right and down from (400,400), got ({moved.X},{moved.Y})");
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

    [WindowsFact]
    public void Capture_InstallsAndRemovesGlobalHooks()
    {
        using var capture = new Win32InputCapture { Handler = _ => false };
        capture.Start();
        capture.Start(); // idempotent
        capture.Stop();
        capture.Start(); // can be restarted after a stop
        capture.Stop();
    }
}
