using System.Collections.Generic;
using Avalonia.Input;
using Mouser.Core.Input;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public class HotkeyBindingTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Esc")]
    [InlineData("Scroll Lock")]
    [InlineData("Win+Alt+V")]
    [InlineData("F13")]
    [InlineData("Shift+Enter")]
    [InlineData("Alt+Space")]
    [InlineData("Ctrl+Shift+Z")]
    [InlineData("Win+Ctrl+Alt+Shift+A")]
    [InlineData("PageUp")]
    [InlineData("PageDown")]
    [InlineData("Home")]
    [InlineData("End")]
    [InlineData("Insert")]
    [InlineData("Delete")]
    [InlineData("Tab")]
    [InlineData("Pause")]
    [InlineData("Up")]
    [InlineData("Down")]
    [InlineData("Left")]
    [InlineData("Right")]
    [InlineData("Backspace")]
    [InlineData("F1")]
    [InlineData("F24")]
    [InlineData("1")]
    [InlineData("9")]
    public void RoundTrip_FormattingAndParsing(string text)
    {
        bool success = HotkeyBinding.TryParse(text, out var b);
        Assert.True(success, $"Failed to parse {text}");
        Assert.False(b.IsEmpty);
        string formatted = b.ToString();
        Assert.Equal(text, formatted);

        bool success2 = HotkeyBinding.TryParse(formatted, out var b2);
        Assert.True(success2);
        Assert.Equal(b, b2);
    }

    [Theory]
    [InlineData("ctrl+alt+esc", 0x1B, true, true, false, false)]
    [InlineData("CONTROL+ALT+ESCAPE", 0x1B, true, true, false, false)]
    [InlineData("windows+alt+v", (ushort)'V', false, true, false, true)]
    [InlineData("super+shift+return", 0x0D, false, false, true, true)]
    [InlineData("scrolllock", 0x91, false, false, false, false)]
    [InlineData("SCROLL LOCK", 0x91, false, false, false, false)]
    public void TryParse_CaseInsensitiveAndAliases(string input, ushort expectedVk, bool ctrl, bool alt, bool shift, bool win)
    {
        bool success = HotkeyBinding.TryParse(input, out var b);
        Assert.True(success);
        Assert.Equal(expectedVk, b.Key);
        Assert.Equal(ctrl, b.Ctrl);
        Assert.Equal(alt, b.Alt);
        Assert.Equal(shift, b.Shift);
        Assert.Equal(win, b.Win);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+")]
    [InlineData("Win+Shift")]
    [InlineData("InvalidKey")]
    [InlineData("Ctrl+Alt")]
    [InlineData("+Esc")]
    [InlineData("Ctrl++Esc")]
    [InlineData("Ctrl+Alt+A+B")]
    [InlineData("F0")]
    [InlineData("F25")]
    public void TryParse_InvalidStrings_ReturnsFalse(string? input)
    {
        bool success = HotkeyBinding.TryParse(input, out var b);
        Assert.False(success);
        Assert.True(b.IsEmpty);
        Assert.Equal(string.Empty, b.ToString());
    }

    [Fact]
    public void Matches_ExactModifierMatch_ReturnsTrue()
    {
        Assert.True(HotkeyBinding.TryParse("Ctrl+Alt+Esc", out var b));

        // Generic modifiers (0x11, 0x12)
        var physicalKeys1 = new HashSet<ushort> { 0x11, 0x12, 0x1B };
        Assert.True(b.Matches(0x1B, physicalKeys1));

        // Left/right modifiers (0xA2, 0xA5)
        var physicalKeys2 = new HashSet<ushort> { 0xA2, 0xA5, 0x1B };
        Assert.True(b.Matches(0x1B, physicalKeys2));

        // Scroll Lock with no modifiers
        Assert.True(HotkeyBinding.TryParse("Scroll Lock", out var scrollLock));
        Assert.True(scrollLock.Matches(0x91, new HashSet<ushort> { 0x91 }));

        // Win+Alt+V
        Assert.True(HotkeyBinding.TryParse("Win+Alt+V", out var winAltV));
        Assert.True(winAltV.Matches((ushort)'V', new HashSet<ushort> { 0x5B, 0xA4, (ushort)'V' }));
    }

    [Fact]
    public void Matches_MissingOrExtraModifiers_ReturnsFalse()
    {
        Assert.True(HotkeyBinding.TryParse("Ctrl+Alt+Esc", out var b));

        // Missing Alt
        Assert.False(b.Matches(0x1B, new HashSet<ushort> { 0x11, 0x1B }));

        // Missing Ctrl
        Assert.False(b.Matches(0x1B, new HashSet<ushort> { 0x12, 0x1B }));

        // Extra Shift
        Assert.False(b.Matches(0x1B, new HashSet<ushort> { 0x11, 0x12, 0x10, 0x1B }));

        // Extra Win
        Assert.False(b.Matches(0x1B, new HashSet<ushort> { 0x11, 0x12, 0x5B, 0x1B }));

        // Wrong Key
        Assert.False(b.Matches((ushort)'A', new HashSet<ushort> { 0x11, 0x12, (ushort)'A' }));
    }

    [Fact]
    public void TryConvertAvaloniaKey_ConvertsCorrectly()
    {
        Assert.True(HotkeysViewModel.TryConvertAvaloniaKey(Key.V, KeyModifiers.Control | KeyModifiers.Alt, out var b1));
        Assert.Equal((ushort)'V', b1.Key);
        Assert.True(b1.Ctrl);
        Assert.True(b1.Alt);
        Assert.False(b1.Shift);
        Assert.False(b1.Win);

        Assert.True(HotkeysViewModel.TryConvertAvaloniaKey(Key.F12, KeyModifiers.Shift, out var b2));
        Assert.Equal(0x7Bu, b2.Key);
        Assert.True(b2.Shift);

        // Modifier-only keys return false
        Assert.False(HotkeysViewModel.TryConvertAvaloniaKey(Key.LeftCtrl, KeyModifiers.Control, out _));
        Assert.False(HotkeysViewModel.TryConvertAvaloniaKey(Key.RightAlt, KeyModifiers.Alt, out _));
        Assert.False(HotkeysViewModel.TryConvertAvaloniaKey(Key.LWin, KeyModifiers.Meta, out _));
    }
}
