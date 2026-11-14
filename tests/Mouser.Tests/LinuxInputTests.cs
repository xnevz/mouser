using Mouser.Core.Input;

namespace Mouser.Tests;

public class LinuxInputTests
{
    [Fact]
    public void X11KeyMap_Letters_MapToAsciiKeysyms()
    {
        // Letters A-Z map to lowercase ASCII keysyms (0x61 - 0x7A)
        for (ushort vk = 0x41; vk <= 0x5A; vk++)
        {
            Assert.True(X11KeyMap.TryGetKeysym(vk, out uint keysym));
            Assert.Equal((uint)(vk + 0x20), keysym);
        }
    }

    [Fact]
    public void X11KeyMap_Digits_MapToAsciiDigits()
    {
        // Digits 0-9 map to ASCII '0'-'9' (0x30 - 0x39)
        for (ushort vk = 0x30; vk <= 0x39; vk++)
        {
            Assert.True(X11KeyMap.TryGetKeysym(vk, out uint keysym));
            Assert.Equal(vk, keysym);
        }
    }

    [Fact]
    public void X11KeyMap_FunctionKeys_MapToX11FunctionKeysyms()
    {
        // F1-F24 map to 0xFFBE - 0xFFD5
        for (ushort i = 0; i < 24; i++)
        {
            ushort vk = (ushort)(0x70 + i);
            Assert.True(X11KeyMap.TryGetKeysym(vk, out uint keysym));
            Assert.Equal(0xFFBEu + i, keysym);
        }
    }

    [Fact]
    public void X11KeyMap_NavigationAndEditing_MapCorrectly()
    {
        Assert.Equal(X11KeyMap.XK_BackSpace, X11KeyMap.VirtualKeyToKeysym(0x08));
        Assert.Equal(X11KeyMap.XK_Tab, X11KeyMap.VirtualKeyToKeysym(0x09));
        Assert.Equal(X11KeyMap.XK_Return, X11KeyMap.VirtualKeyToKeysym(0x0D));
        Assert.Equal(X11KeyMap.XK_Escape, X11KeyMap.VirtualKeyToKeysym(0x1B));
        Assert.Equal(X11KeyMap.XK_space, X11KeyMap.VirtualKeyToKeysym(0x20));
        Assert.Equal(X11KeyMap.XK_Page_Up, X11KeyMap.VirtualKeyToKeysym(0x21));
        Assert.Equal(X11KeyMap.XK_Page_Down, X11KeyMap.VirtualKeyToKeysym(0x22));
        Assert.Equal(X11KeyMap.XK_End, X11KeyMap.VirtualKeyToKeysym(0x23));
        Assert.Equal(X11KeyMap.XK_Home, X11KeyMap.VirtualKeyToKeysym(0x24));
        Assert.Equal(X11KeyMap.XK_Left, X11KeyMap.VirtualKeyToKeysym(0x25));
        Assert.Equal(X11KeyMap.XK_Up, X11KeyMap.VirtualKeyToKeysym(0x26));
        Assert.Equal(X11KeyMap.XK_Right, X11KeyMap.VirtualKeyToKeysym(0x27));
        Assert.Equal(X11KeyMap.XK_Down, X11KeyMap.VirtualKeyToKeysym(0x28));
        Assert.Equal(X11KeyMap.XK_Insert, X11KeyMap.VirtualKeyToKeysym(0x2D));
        Assert.Equal(X11KeyMap.XK_Delete, X11KeyMap.VirtualKeyToKeysym(0x2E));
    }

    [Fact]
    public void X11KeyMap_Modifiers_MapCorrectly()
    {
        Assert.Equal(X11KeyMap.XK_Shift_L, X11KeyMap.VirtualKeyToKeysym(0x10));
        Assert.Equal(X11KeyMap.XK_Shift_L, X11KeyMap.VirtualKeyToKeysym(0xA0));
        Assert.Equal(X11KeyMap.XK_Shift_R, X11KeyMap.VirtualKeyToKeysym(0xA1));
        Assert.Equal(X11KeyMap.XK_Control_L, X11KeyMap.VirtualKeyToKeysym(0x11));
        Assert.Equal(X11KeyMap.XK_Control_L, X11KeyMap.VirtualKeyToKeysym(0xA2));
        Assert.Equal(X11KeyMap.XK_Control_R, X11KeyMap.VirtualKeyToKeysym(0xA3));
        Assert.Equal(X11KeyMap.XK_Alt_L, X11KeyMap.VirtualKeyToKeysym(0x12));
        Assert.Equal(X11KeyMap.XK_Alt_L, X11KeyMap.VirtualKeyToKeysym(0xA4));
        Assert.Equal(X11KeyMap.XK_Alt_R, X11KeyMap.VirtualKeyToKeysym(0xA5));
        Assert.Equal(X11KeyMap.XK_Super_L, X11KeyMap.VirtualKeyToKeysym(0x5B));
        Assert.Equal(X11KeyMap.XK_Super_R, X11KeyMap.VirtualKeyToKeysym(0x5C));
        Assert.Equal(X11KeyMap.XK_Caps_Lock, X11KeyMap.VirtualKeyToKeysym(0x14));
        Assert.Equal(X11KeyMap.XK_Num_Lock, X11KeyMap.VirtualKeyToKeysym(0x90));
        Assert.Equal(X11KeyMap.XK_Scroll_Lock, X11KeyMap.VirtualKeyToKeysym(0x91));
    }

    [Fact]
    public void X11KeyMap_MediaKeys_MapCorrectly()
    {
        Assert.Equal(X11KeyMap.XF86AudioMute, X11KeyMap.VirtualKeyToKeysym(0xAD));
        Assert.Equal(X11KeyMap.XF86AudioLowerVolume, X11KeyMap.VirtualKeyToKeysym(0xAE));
        Assert.Equal(X11KeyMap.XF86AudioRaiseVolume, X11KeyMap.VirtualKeyToKeysym(0xAF));
        Assert.Equal(X11KeyMap.XF86AudioNext, X11KeyMap.VirtualKeyToKeysym(0xB0));
        Assert.Equal(X11KeyMap.XF86AudioPrev, X11KeyMap.VirtualKeyToKeysym(0xB1));
        Assert.Equal(X11KeyMap.XF86AudioStop, X11KeyMap.VirtualKeyToKeysym(0xB2));
        Assert.Equal(X11KeyMap.XF86AudioPlay, X11KeyMap.VirtualKeyToKeysym(0xB3));
    }

    [Fact]
    public void X11KeyMap_NumpadOperators_MapCorrectly()
    {
        Assert.Equal(0xFFAAu, X11KeyMap.VirtualKeyToKeysym(0x6A)); // Multiply
        Assert.Equal(0xFFABu, X11KeyMap.VirtualKeyToKeysym(0x6B)); // Add
        Assert.Equal(0xFFADu, X11KeyMap.VirtualKeyToKeysym(0x6D)); // Subtract
        Assert.Equal(0xFFAEu, X11KeyMap.VirtualKeyToKeysym(0x6E)); // Decimal
        Assert.Equal(0xFFAFu, X11KeyMap.VirtualKeyToKeysym(0x6F)); // Divide
    }

    [Fact]
    public void X11InputSink_InstantiatesSafelyOnNonLinux_IsSupportedFalse()
    {
        using var sink = new X11InputSink();
        if (!OperatingSystem.IsLinux())
        {
            Assert.False(sink.IsSupported);
            Assert.NotNull(sink.UnsupportedReason);

            // Safe no-op calls
            sink.MoveRelative(10, 10);
            sink.MoveAbsolute(100, 100);
            sink.SetButton(MouseButton.Left, true);
            sink.Scroll(0, 120);
            sink.Key(0x41, true);
            sink.Char(0x41, true);
        }
    }
}
