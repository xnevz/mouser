using System;
using System.IO;
using Avalonia.Input;
using Mouser.Core.Input;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public class HotkeysViewModelTests : IDisposable
{
    private readonly string _tempFile;
    private readonly SettingsStore _store;
    private readonly MouserHost _host;
    private readonly HotkeysViewModel _vm;

    public HotkeysViewModelTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"mouser_hotkeys_{Guid.NewGuid():N}.json");
        _store = new SettingsStore(_tempFile);
        _host = new MouserHost(_store, new RecordingSink(), new FakeCapture());
        _vm = new HotkeysViewModel(_store, _host);
    }

    public void Dispose()
    {
        _host.Dispose();
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public void Defaults_MatchExpectedValues()
    {
        Assert.Equal("Ctrl+Alt+Esc", _vm.ReturnControl);
        Assert.Equal("Ctrl+Alt+Esc", _vm.ReturnControlDisplay);
        Assert.Equal("", _vm.LockCursor);
        Assert.Equal("None", _vm.LockCursorDisplay);
        Assert.Equal("Win+Alt+V", _vm.QuickPaste);
        Assert.Equal("Win+Alt+V", _vm.QuickPasteDisplay);
        Assert.Equal("Ctrl+Alt+Right", _vm.NextComputer);
        Assert.Equal("Ctrl+Alt+Right", _vm.NextComputerDisplay);
        Assert.Equal("Ctrl+Alt+1", _vm.Jump1);
        Assert.Equal("Ctrl+Alt+1", _vm.Jump1Display);
        Assert.Equal("Ctrl+Alt+2", _vm.Jump2);
        Assert.Equal("Ctrl+Alt+3", _vm.Jump3);
        Assert.Equal("", _vm.Jump4);
        Assert.Equal("None", _vm.Jump4Display);
        Assert.True(_vm.PanicDoublePressCtrl);
        Assert.False(_vm.IsListening);
    }

    [Fact]
    public void ListeningMode_ToggleAndEscapeToCancel()
    {
        _vm.ToggleListeningReturnControlCommand.Execute(null);
        Assert.True(_vm.IsListening);
        Assert.True(_vm.IsListeningReturnControl);
        Assert.Contains("Esc to cancel", _vm.ReturnControlButtonText);

        // Escape cancels listening
        bool handled = _vm.HandleKey(Key.Escape, KeyModifiers.None);
        Assert.True(handled);
        Assert.False(_vm.IsListening);
        Assert.Equal("Change", _vm.ReturnControlButtonText);
        Assert.Equal("Ctrl+Alt+Esc", _vm.ReturnControl);
    }

    [Fact]
    public void ListeningMode_BackspaceClearsHotkey()
    {
        _vm.ToggleListeningLockCursorCommand.Execute(null);
        Assert.True(_vm.IsListeningLockCursor);

        bool handled = _vm.HandleKey(Key.Back, KeyModifiers.None);
        Assert.True(handled);
        Assert.False(_vm.IsListening);
        Assert.Equal("", _vm.LockCursor);
        Assert.Equal("None", _vm.LockCursorDisplay);
    }

    [Fact]
    public void ListeningMode_ModifierOnly_DoesNotCommit()
    {
        _vm.ToggleListeningReturnControlCommand.Execute(null);
        Assert.True(_vm.IsListening);

        // Pressing Ctrl or Alt only keeps waiting
        Assert.True(_vm.HandleKey(Key.LeftCtrl, KeyModifiers.Control));
        Assert.True(_vm.IsListening);
        Assert.Equal("Ctrl+Alt+Esc", _vm.ReturnControl);
    }

    [Fact]
    public void ListeningMode_CapturesAndSavesValidHotkey()
    {
        _vm.ToggleListeningReturnControlCommand.Execute(null);

        // Press Alt+X
        bool handled = _vm.HandleKey(Key.X, KeyModifiers.Alt);
        Assert.True(handled);
        Assert.False(_vm.IsListening);
        Assert.Equal("Alt+X", _vm.ReturnControl);
        Assert.Equal("Alt+X", _store.Current.Hotkeys.ReturnControl);

        // Verify reloaded store has it
        var store2 = new SettingsStore(_tempFile);
        Assert.Equal("Alt+X", store2.Current.Hotkeys.ReturnControl);
    }

    [Fact]
    public void ConflictDetection_PreventsDuplicateHotkeys()
    {
        _vm.ToggleListeningLockCursorCommand.Execute(null);

        // Try to bind ReturnControl hotkey ("Ctrl+Alt+Esc") to LockCursor
        bool handled = _vm.HandleKey(Key.Escape, KeyModifiers.Control | KeyModifiers.Alt);
        Assert.True(handled);
        Assert.NotNull(_vm.ConflictMessage);
        Assert.Contains("Already used by Return control", _vm.ConflictMessage);
        Assert.Equal("", _vm.LockCursor); // Not changed
    }

    [Fact]
    public void ResetCommands_RestoreDefaults()
    {
        _vm.ReturnControl = "F11";
        _vm.LockCursor = "F12";
        _vm.QuickPaste = "F10";

        _vm.ResetReturnControlCommand.Execute(null);
        Assert.Equal("Ctrl+Alt+Esc", _vm.ReturnControl);

        _vm.ClearLockCursorCommand.Execute(null);
        Assert.Equal("", _vm.LockCursor);

        _vm.ResetQuickPasteCommand.Execute(null);
        Assert.Equal("Win+Alt+V", _vm.QuickPaste);
    }

    [Fact]
    public void PanicDoublePressCtrl_Toggle_SavesAndApplies()
    {
        _vm.PanicDoublePressCtrl = false;
        Assert.False(_store.Current.Hotkeys.PanicDoublePressCtrl);
        Assert.False(_host.Kvm!.Options.PanicDoublePressCtrl);

        _vm.PanicDoublePressCtrl = true;
        Assert.True(_store.Current.Hotkeys.PanicDoublePressCtrl);
        Assert.True(_host.Kvm!.Options.PanicDoublePressCtrl);
    }

    [Fact]
    public void NextComputerAndJump_ListeningAndResetAndConflict()
    {
        // Toggle listening next computer
        _vm.ToggleListeningNextComputerCommand.Execute(null);
        Assert.True(_vm.IsListening);
        Assert.True(_vm.IsListeningNextComputer);

        // Conflict check: trying to set NextComputer to "Ctrl+Alt+1" (which Jump1 uses)
        bool handled = _vm.HandleKey(Key.D1, KeyModifiers.Control | KeyModifiers.Alt);
        Assert.True(handled);
        Assert.NotNull(_vm.ConflictMessage);
        Assert.Contains("Already used by Jump to computer 1", _vm.ConflictMessage);

        // Cancel
        _vm.CancelListeningCommand.Execute(null);
        Assert.False(_vm.IsListening);

        // Clear command
        _vm.ClearNextComputerCommand.Execute(null);
        Assert.Equal("", _vm.NextComputer);
        Assert.Equal("None", _vm.NextComputerDisplay);

        // Reset command
        _vm.ResetNextComputerCommand.Execute(null);
        Assert.Equal("Ctrl+Alt+Right", _vm.NextComputer);

        // Jump1 reset and clear
        _vm.ClearJump1Command.Execute(null);
        Assert.Equal("", _vm.Jump1);
        _vm.ResetJump1Command.Execute(null);
        Assert.Equal("Ctrl+Alt+1", _vm.Jump1);
    }
}
