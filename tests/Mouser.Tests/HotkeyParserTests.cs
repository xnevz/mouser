using Mouser.Core.Clipboard;

namespace Mouser.Tests;

public class HotkeyParserTests
{
    [Fact]
    public void TryParse_StandardWinAltV_ReturnsTrueWithCorrectModifiersAndVk()
    {
        bool success = HotkeyParser.TryParse("Win+Alt+V", out uint mods, out uint vk);
        Assert.True(success);
        Assert.Equal(HotkeyParser.MOD_WIN | HotkeyParser.MOD_ALT, mods);
        Assert.Equal((uint)'V', vk);
    }

    [Theory]
    [InlineData("Ctrl+A", HotkeyParser.MOD_CONTROL, (uint)'A')]
    [InlineData("ctrl+z", HotkeyParser.MOD_CONTROL, (uint)'Z')]
    [InlineData("Shift+Alt+m", HotkeyParser.MOD_SHIFT | HotkeyParser.MOD_ALT, (uint)'M')]
    [InlineData("WIN+CONTROL+X", HotkeyParser.MOD_WIN | HotkeyParser.MOD_CONTROL, (uint)'X')]
    public void TryParse_Letters_CaseInsensitive(string input, uint expectedMods, uint expectedVk)
    {
        bool success = HotkeyParser.TryParse(input, out uint mods, out uint vk);
        Assert.True(success);
        Assert.Equal(expectedMods, mods);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData("Alt+1", HotkeyParser.MOD_ALT, (uint)'1')]
    [InlineData("Ctrl+Alt+9", HotkeyParser.MOD_CONTROL | HotkeyParser.MOD_ALT, (uint)'9')]
    [InlineData("Win+0", HotkeyParser.MOD_WIN, (uint)'0')]
    public void TryParse_Digits_ReturnsTrue(string input, uint expectedMods, uint expectedVk)
    {
        bool success = HotkeyParser.TryParse(input, out uint mods, out uint vk);
        Assert.True(success);
        Assert.Equal(expectedMods, mods);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData("F1", 0u, 0x70u)]
    [InlineData("Ctrl+F5", HotkeyParser.MOD_CONTROL, 0x74u)]
    [InlineData("Alt+F12", HotkeyParser.MOD_ALT, 0x7Bu)]
    [InlineData("Shift+F13", HotkeyParser.MOD_SHIFT, 0x7Cu)]
    [InlineData("Win+Alt+F24", HotkeyParser.MOD_WIN | HotkeyParser.MOD_ALT, 0x87u)]
    public void TryParse_FunctionKeys_F1ThroughF24(string input, uint expectedMods, uint expectedVk)
    {
        bool success = HotkeyParser.TryParse(input, out uint mods, out uint vk);
        Assert.True(success);
        Assert.Equal(expectedMods, mods);
        Assert.Equal(expectedVk, vk);
    }

    [Fact]
    public void TryParse_ModifierAliases_ControlAndSuperAndWindows()
    {
        Assert.True(HotkeyParser.TryParse("Control+Super+C", out uint mods1, out uint vk1));
        Assert.Equal(HotkeyParser.MOD_CONTROL | HotkeyParser.MOD_WIN, mods1);
        Assert.Equal((uint)'C', vk1);

        Assert.True(HotkeyParser.TryParse("Windows+Alt+Space", out uint mods2, out uint vk2));
        Assert.Equal(HotkeyParser.MOD_WIN | HotkeyParser.MOD_ALT, mods2);
        Assert.Equal(0x20u, vk2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+")]
    [InlineData("+V")]
    [InlineData("Ctrl++V")]
    [InlineData("Ctrl+Alt")] // No key
    [InlineData("Ctrl+Alt+InvalidKey")]
    [InlineData("Ctrl+A+B")] // Two keys
    [InlineData("F25")] // F25 does not exist
    [InlineData("F0")]
    public void TryParse_InvalidHotkeys_ReturnsFalse(string? input)
    {
        bool success = HotkeyParser.TryParse(input, out uint mods, out uint vk);
        Assert.False(success);
        Assert.Equal(0u, mods);
        Assert.Equal(0u, vk);
    }
}
