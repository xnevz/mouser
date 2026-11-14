using System;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Input;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

public enum HotkeyTarget
{
    None,
    ReturnControl,
    LockCursor,
    QuickPaste
}

public partial class HotkeysViewModel : ViewModelBase
{
    private readonly SettingsStore _settings;
    private readonly MouserHost _host;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReturnControlDisplay))]
    private string _returnControl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LockCursorDisplay))]
    private string _lockCursor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuickPasteDisplay))]
    private string _quickPaste;

    [ObservableProperty]
    private bool _panicDoublePressCtrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsListening),
        nameof(IsListeningReturnControl),
        nameof(IsListeningLockCursor),
        nameof(IsListeningQuickPaste),
        nameof(ReturnControlButtonText),
        nameof(LockCursorButtonText),
        nameof(QuickPasteButtonText))]
    private HotkeyTarget _activeListening = HotkeyTarget.None;

    [ObservableProperty]
    private string? _conflictMessage;

    public event Action? QuickPasteHotkeyChanged;

    public bool IsListening => ActiveListening != HotkeyTarget.None;
    public bool IsListeningReturnControl => ActiveListening == HotkeyTarget.ReturnControl;
    public bool IsListeningLockCursor => ActiveListening == HotkeyTarget.LockCursor;
    public bool IsListeningQuickPaste => ActiveListening == HotkeyTarget.QuickPaste;

    public string ReturnControlDisplay => string.IsNullOrWhiteSpace(ReturnControl) ? "None" : ReturnControl;
    public string LockCursorDisplay => string.IsNullOrWhiteSpace(LockCursor) ? "None" : LockCursor;
    public string QuickPasteDisplay => string.IsNullOrWhiteSpace(QuickPaste) ? "None" : QuickPaste;

    public string ReturnControlButtonText => IsListeningReturnControl ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string LockCursorButtonText => IsListeningLockCursor ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string QuickPasteButtonText => IsListeningQuickPaste ? "Press keys... Esc to cancel, Backspace to clear" : "Change";

    public HotkeysViewModel(SettingsStore settings, MouserHost host)
    {
        _settings = settings;
        _host = host;

        var h = settings.Current.Hotkeys;
        _returnControl = h.ReturnControl;
        _lockCursor = h.LockCursor;
        _quickPaste = h.QuickPaste;
        _panicDoublePressCtrl = h.PanicDoublePressCtrl;
    }

    [RelayCommand]
    private void ToggleListeningReturnControl()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.ReturnControl ? HotkeyTarget.None : HotkeyTarget.ReturnControl;
    }

    [RelayCommand]
    private void ResetReturnControl()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        ReturnControl = "Ctrl+Alt+Esc";
        _settings.Current.Hotkeys.ReturnControl = ReturnControl;
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ToggleListeningLockCursor()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.LockCursor ? HotkeyTarget.None : HotkeyTarget.LockCursor;
    }

    [RelayCommand]
    private void ClearLockCursor()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        LockCursor = "";
        _settings.Current.Hotkeys.LockCursor = "";
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ToggleListeningQuickPaste()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.QuickPaste ? HotkeyTarget.None : HotkeyTarget.QuickPaste;
    }

    [RelayCommand]
    private void ResetQuickPaste()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        QuickPaste = "Win+Alt+V";
        _settings.Current.Hotkeys.QuickPaste = QuickPaste;
        _settings.Save();
        _host.ApplyKvmOptions();
        QuickPasteHotkeyChanged?.Invoke();
    }

    partial void OnPanicDoublePressCtrlChanged(bool value)
    {
        _settings.Current.Hotkeys.PanicDoublePressCtrl = value;
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    public void CancelListening()
    {
        ActiveListening = HotkeyTarget.None;
        ConflictMessage = null;
    }

    public void ClearCurrentListening()
    {
        switch (ActiveListening)
        {
            case HotkeyTarget.ReturnControl:
                ReturnControl = "";
                _settings.Current.Hotkeys.ReturnControl = "";
                _settings.Save();
                _host.ApplyKvmOptions();
                break;
            case HotkeyTarget.LockCursor:
                LockCursor = "";
                _settings.Current.Hotkeys.LockCursor = "";
                _settings.Save();
                _host.ApplyKvmOptions();
                break;
            case HotkeyTarget.QuickPaste:
                QuickPaste = "";
                _settings.Current.Hotkeys.QuickPaste = "";
                _settings.Save();
                _host.ApplyKvmOptions();
                QuickPasteHotkeyChanged?.Invoke();
                break;
        }
        ActiveListening = HotkeyTarget.None;
        ConflictMessage = null;
    }

    public bool CommitBinding(HotkeyBinding binding)
    {
        string text = binding.ToString();
        var target = ActiveListening;

        if (target == HotkeyTarget.ReturnControl)
        {
            if (HotkeyBinding.TryParse(QuickPaste, out var qp) && qp == binding)
            {
                ConflictMessage = "Already used by Quick paste overlay";
                return false;
            }
            if (HotkeyBinding.TryParse(LockCursor, out var lc) && lc == binding)
            {
                ConflictMessage = "Already used by Lock cursor";
                return false;
            }
            ReturnControl = text;
            _settings.Current.Hotkeys.ReturnControl = text;
            _settings.Save();
            _host.ApplyKvmOptions();
        }
        else if (target == HotkeyTarget.LockCursor)
        {
            if (HotkeyBinding.TryParse(ReturnControl, out var rc) && rc == binding)
            {
                ConflictMessage = "Already used by Return control";
                return false;
            }
            if (HotkeyBinding.TryParse(QuickPaste, out var qp) && qp == binding)
            {
                ConflictMessage = "Already used by Quick paste overlay";
                return false;
            }
            LockCursor = text;
            _settings.Current.Hotkeys.LockCursor = text;
            _settings.Save();
            _host.ApplyKvmOptions();
        }
        else if (target == HotkeyTarget.QuickPaste)
        {
            if (HotkeyBinding.TryParse(ReturnControl, out var rc) && rc == binding)
            {
                ConflictMessage = "Already used by Return control";
                return false;
            }
            if (HotkeyBinding.TryParse(LockCursor, out var lc) && lc == binding)
            {
                ConflictMessage = "Already used by Lock cursor";
                return false;
            }
            QuickPaste = text;
            _settings.Current.Hotkeys.QuickPaste = text;
            _settings.Save();
            _host.ApplyKvmOptions();
            QuickPasteHotkeyChanged?.Invoke();
        }

        ActiveListening = HotkeyTarget.None;
        ConflictMessage = null;
        return true;
    }

    public bool HandleKey(Key key, KeyModifiers modifiers)
    {
        if (ActiveListening == HotkeyTarget.None) return false;

        if (key == Key.Escape && modifiers == KeyModifiers.None)
        {
            CancelListening();
            return true;
        }

        if (key == Key.Back && modifiers == KeyModifiers.None)
        {
            ClearCurrentListening();
            return true;
        }

        if (IsModifierKey(key))
        {
            return true;
        }

        if (TryConvertAvaloniaKey(key, modifiers, out var binding))
        {
            CommitBinding(binding);
            return true;
        }

        return false;
    }

    public static bool IsModifierKey(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl => true,
        Key.LeftAlt or Key.RightAlt => true,
        Key.LeftShift or Key.RightShift => true,
        Key.LWin or Key.RWin => true,
        _ => false
    };

    public static bool TryGetVirtualKey(Key key, out ushort vk)
    {
        vk = key switch
        {
            >= Key.A and <= Key.Z => (ushort)(0x41 + (key - Key.A)),
            >= Key.D0 and <= Key.D9 => (ushort)(0x30 + (key - Key.D0)),
            >= Key.NumPad0 and <= Key.NumPad9 => (ushort)(0x30 + (key - Key.NumPad0)),
            >= Key.F1 and <= Key.F24 => (ushort)(0x70 + (key - Key.F1)),
            Key.Escape => 0x1B,
            Key.Tab => 0x09,
            Key.Space => 0x20,
            Key.Enter or Key.Return => 0x0D,
            Key.Back => 0x08,
            Key.Insert => 0x2D,
            Key.Delete => 0x2E,
            Key.Home => 0x24,
            Key.End => 0x23,
            Key.PageUp => 0x21,
            Key.PageDown => 0x22,
            Key.Up => 0x26,
            Key.Down => 0x28,
            Key.Left => 0x25,
            Key.Right => 0x27,
            Key.Scroll => 0x91,
            Key.Pause => 0x13,
            _ => 0
        };
        return vk != 0;
    }

    public static bool TryConvertAvaloniaKey(Key key, KeyModifiers modifiers, out HotkeyBinding binding)
    {
        binding = default;
        if (IsModifierKey(key)) return false;
        if (!TryGetVirtualKey(key, out ushort vk)) return false;

        bool ctrl = (modifiers & KeyModifiers.Control) != 0;
        bool alt = (modifiers & KeyModifiers.Alt) != 0;
        bool shift = (modifiers & KeyModifiers.Shift) != 0;
        bool win = (modifiers & KeyModifiers.Meta) != 0;

        binding = new HotkeyBinding(vk, ctrl, alt, shift, win);
        return true;
    }
}
