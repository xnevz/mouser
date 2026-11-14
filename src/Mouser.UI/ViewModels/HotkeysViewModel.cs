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
    QuickPaste,
    PasteFromPeer,
    NextComputer,
    Jump1,
    Jump2,
    Jump3,
    Jump4,
    Jump5,
    Jump6,
    Jump7,
    Jump8,
    Jump9
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
    [NotifyPropertyChangedFor(nameof(PasteFromPeerDisplay))]
    private string _pasteFromPeer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NextComputerDisplay))]
    private string _nextComputer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Jump1Display))]
    private string _jump1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Jump2Display))]
    private string _jump2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Jump3Display))]
    private string _jump3;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Jump4Display))]
    private string _jump4;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Jump5Display))]
    private string _jump5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Jump6Display))]
    private string _jump6;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Jump7Display))]
    private string _jump7;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Jump8Display))]
    private string _jump8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Jump9Display))]
    private string _jump9;

    [ObservableProperty]
    private bool _panicDoublePressCtrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsListening),
        nameof(IsListeningReturnControl),
        nameof(IsListeningLockCursor),
        nameof(IsListeningQuickPaste),
        nameof(IsListeningPasteFromPeer),
        nameof(IsListeningNextComputer),
        nameof(IsListeningJump1),
        nameof(IsListeningJump2),
        nameof(IsListeningJump3),
        nameof(IsListeningJump4),
        nameof(IsListeningJump5),
        nameof(IsListeningJump6),
        nameof(IsListeningJump7),
        nameof(IsListeningJump8),
        nameof(IsListeningJump9),
        nameof(ReturnControlButtonText),
        nameof(LockCursorButtonText),
        nameof(QuickPasteButtonText),
        nameof(PasteFromPeerButtonText),
        nameof(NextComputerButtonText),
        nameof(Jump1ButtonText),
        nameof(Jump2ButtonText),
        nameof(Jump3ButtonText),
        nameof(Jump4ButtonText),
        nameof(Jump5ButtonText),
        nameof(Jump6ButtonText),
        nameof(Jump7ButtonText),
        nameof(Jump8ButtonText),
        nameof(Jump9ButtonText))]
    private HotkeyTarget _activeListening = HotkeyTarget.None;

    [ObservableProperty]
    private string? _conflictMessage;

    public event Action? QuickPasteHotkeyChanged;
    public event Action? PasteFromPeerHotkeyChanged;

    public bool IsListening => ActiveListening != HotkeyTarget.None;
    public bool IsListeningReturnControl => ActiveListening == HotkeyTarget.ReturnControl;
    public bool IsListeningLockCursor => ActiveListening == HotkeyTarget.LockCursor;
    public bool IsListeningQuickPaste => ActiveListening == HotkeyTarget.QuickPaste;
    public bool IsListeningPasteFromPeer => ActiveListening == HotkeyTarget.PasteFromPeer;
    public bool IsListeningNextComputer => ActiveListening == HotkeyTarget.NextComputer;
    public bool IsListeningJump1 => ActiveListening == HotkeyTarget.Jump1;
    public bool IsListeningJump2 => ActiveListening == HotkeyTarget.Jump2;
    public bool IsListeningJump3 => ActiveListening == HotkeyTarget.Jump3;
    public bool IsListeningJump4 => ActiveListening == HotkeyTarget.Jump4;
    public bool IsListeningJump5 => ActiveListening == HotkeyTarget.Jump5;
    public bool IsListeningJump6 => ActiveListening == HotkeyTarget.Jump6;
    public bool IsListeningJump7 => ActiveListening == HotkeyTarget.Jump7;
    public bool IsListeningJump8 => ActiveListening == HotkeyTarget.Jump8;
    public bool IsListeningJump9 => ActiveListening == HotkeyTarget.Jump9;

    public string ReturnControlDisplay => string.IsNullOrWhiteSpace(ReturnControl) ? "None" : ReturnControl;
    public string LockCursorDisplay => string.IsNullOrWhiteSpace(LockCursor) ? "None" : LockCursor;
    public string QuickPasteDisplay => string.IsNullOrWhiteSpace(QuickPaste) ? "None" : QuickPaste;
    public string PasteFromPeerDisplay => string.IsNullOrWhiteSpace(PasteFromPeer) ? "None" : PasteFromPeer;
    public string NextComputerDisplay => string.IsNullOrWhiteSpace(NextComputer) ? "None" : NextComputer;
    public string Jump1Display => string.IsNullOrWhiteSpace(Jump1) ? "None" : Jump1;
    public string Jump2Display => string.IsNullOrWhiteSpace(Jump2) ? "None" : Jump2;
    public string Jump3Display => string.IsNullOrWhiteSpace(Jump3) ? "None" : Jump3;
    public string Jump4Display => string.IsNullOrWhiteSpace(Jump4) ? "None" : Jump4;
    public string Jump5Display => string.IsNullOrWhiteSpace(Jump5) ? "None" : Jump5;
    public string Jump6Display => string.IsNullOrWhiteSpace(Jump6) ? "None" : Jump6;
    public string Jump7Display => string.IsNullOrWhiteSpace(Jump7) ? "None" : Jump7;
    public string Jump8Display => string.IsNullOrWhiteSpace(Jump8) ? "None" : Jump8;
    public string Jump9Display => string.IsNullOrWhiteSpace(Jump9) ? "None" : Jump9;

    public string ReturnControlButtonText => IsListeningReturnControl ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string LockCursorButtonText => IsListeningLockCursor ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string QuickPasteButtonText => IsListeningQuickPaste ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string PasteFromPeerButtonText => IsListeningPasteFromPeer ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string NextComputerButtonText => IsListeningNextComputer ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string Jump1ButtonText => IsListeningJump1 ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string Jump2ButtonText => IsListeningJump2 ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string Jump3ButtonText => IsListeningJump3 ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string Jump4ButtonText => IsListeningJump4 ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string Jump5ButtonText => IsListeningJump5 ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string Jump6ButtonText => IsListeningJump6 ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string Jump7ButtonText => IsListeningJump7 ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string Jump8ButtonText => IsListeningJump8 ? "Press keys... Esc to cancel, Backspace to clear" : "Change";
    public string Jump9ButtonText => IsListeningJump9 ? "Press keys... Esc to cancel, Backspace to clear" : "Change";

    public HotkeysViewModel(SettingsStore settings, MouserHost host)
    {
        _settings = settings;
        _host = host;

        var h = settings.Current.Hotkeys;
        _returnControl = h.ReturnControl;
        _lockCursor = h.LockCursor;
        _quickPaste = h.QuickPaste;
        _pasteFromPeer = h.PasteFromPeer;
        _panicDoublePressCtrl = h.PanicDoublePressCtrl;
        _nextComputer = h.NextComputer;
        _jump1 = h.Jump1;
        _jump2 = h.Jump2;
        _jump3 = h.Jump3;
        _jump4 = h.Jump4;
        _jump5 = h.Jump5;
        _jump6 = h.Jump6;
        _jump7 = h.Jump7;
        _jump8 = h.Jump8;
        _jump9 = h.Jump9;
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

    [RelayCommand]
    private void ToggleListeningPasteFromPeer()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.PasteFromPeer ? HotkeyTarget.None : HotkeyTarget.PasteFromPeer;
    }

    [RelayCommand]
    private void ResetPasteFromPeer()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        PasteFromPeer = "Ctrl+Shift+V";
        _settings.Current.Hotkeys.PasteFromPeer = PasteFromPeer;
        _settings.Save();
        _host.ApplyKvmOptions();
        PasteFromPeerHotkeyChanged?.Invoke();
    }

    [RelayCommand]
    private void ToggleListeningNextComputer()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.NextComputer ? HotkeyTarget.None : HotkeyTarget.NextComputer;
    }

    [RelayCommand]
    private void ResetNextComputer()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        NextComputer = "Ctrl+Alt+Right";
        _settings.Current.Hotkeys.NextComputer = NextComputer;
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ClearNextComputer()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        NextComputer = "";
        _settings.Current.Hotkeys.NextComputer = "";
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ToggleListeningJump1()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.Jump1 ? HotkeyTarget.None : HotkeyTarget.Jump1;
    }

    [RelayCommand]
    private void ResetJump1()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        Jump1 = "Ctrl+Alt+1";
        _settings.Current.Hotkeys.Jump1 = Jump1;
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ClearJump1()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        Jump1 = "";
        _settings.Current.Hotkeys.Jump1 = "";
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ToggleListeningJump2()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.Jump2 ? HotkeyTarget.None : HotkeyTarget.Jump2;
    }

    [RelayCommand]
    private void ResetJump2()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        Jump2 = "Ctrl+Alt+2";
        _settings.Current.Hotkeys.Jump2 = Jump2;
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ClearJump2()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        Jump2 = "";
        _settings.Current.Hotkeys.Jump2 = "";
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ToggleListeningJump3()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.Jump3 ? HotkeyTarget.None : HotkeyTarget.Jump3;
    }

    [RelayCommand]
    private void ResetJump3()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        Jump3 = "Ctrl+Alt+3";
        _settings.Current.Hotkeys.Jump3 = Jump3;
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ClearJump3()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        Jump3 = "";
        _settings.Current.Hotkeys.Jump3 = "";
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ToggleListeningJump4()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.Jump4 ? HotkeyTarget.None : HotkeyTarget.Jump4;
    }

    [RelayCommand]
    private void ClearJump4()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        Jump4 = "";
        _settings.Current.Hotkeys.Jump4 = "";
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ToggleListeningJump5()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.Jump5 ? HotkeyTarget.None : HotkeyTarget.Jump5;
    }

    [RelayCommand]
    private void ClearJump5()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        Jump5 = "";
        _settings.Current.Hotkeys.Jump5 = "";
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ToggleListeningJump6()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.Jump6 ? HotkeyTarget.None : HotkeyTarget.Jump6;
    }

    [RelayCommand]
    private void ClearJump6()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        Jump6 = "";
        _settings.Current.Hotkeys.Jump6 = "";
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ToggleListeningJump7()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.Jump7 ? HotkeyTarget.None : HotkeyTarget.Jump7;
    }

    [RelayCommand]
    private void ClearJump7()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        Jump7 = "";
        _settings.Current.Hotkeys.Jump7 = "";
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ToggleListeningJump8()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.Jump8 ? HotkeyTarget.None : HotkeyTarget.Jump8;
    }

    [RelayCommand]
    private void ClearJump8()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        Jump8 = "";
        _settings.Current.Hotkeys.Jump8 = "";
        _settings.Save();
        _host.ApplyKvmOptions();
    }

    [RelayCommand]
    private void ToggleListeningJump9()
    {
        ConflictMessage = null;
        ActiveListening = ActiveListening == HotkeyTarget.Jump9 ? HotkeyTarget.None : HotkeyTarget.Jump9;
    }

    [RelayCommand]
    private void ClearJump9()
    {
        ConflictMessage = null;
        ActiveListening = HotkeyTarget.None;
        Jump9 = "";
        _settings.Current.Hotkeys.Jump9 = "";
        _settings.Save();
        _host.ApplyKvmOptions();
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
                break;
            case HotkeyTarget.LockCursor:
                LockCursor = "";
                _settings.Current.Hotkeys.LockCursor = "";
                break;
            case HotkeyTarget.QuickPaste:
                QuickPaste = "";
                _settings.Current.Hotkeys.QuickPaste = "";
                QuickPasteHotkeyChanged?.Invoke();
                break;
            case HotkeyTarget.PasteFromPeer:
                PasteFromPeer = "";
                _settings.Current.Hotkeys.PasteFromPeer = "";
                PasteFromPeerHotkeyChanged?.Invoke();
                break;
            case HotkeyTarget.NextComputer:
                NextComputer = "";
                _settings.Current.Hotkeys.NextComputer = "";
                break;
            case HotkeyTarget.Jump1:
                Jump1 = "";
                _settings.Current.Hotkeys.Jump1 = "";
                break;
            case HotkeyTarget.Jump2:
                Jump2 = "";
                _settings.Current.Hotkeys.Jump2 = "";
                break;
            case HotkeyTarget.Jump3:
                Jump3 = "";
                _settings.Current.Hotkeys.Jump3 = "";
                break;
            case HotkeyTarget.Jump4:
                Jump4 = "";
                _settings.Current.Hotkeys.Jump4 = "";
                break;
            case HotkeyTarget.Jump5:
                Jump5 = "";
                _settings.Current.Hotkeys.Jump5 = "";
                break;
            case HotkeyTarget.Jump6:
                Jump6 = "";
                _settings.Current.Hotkeys.Jump6 = "";
                break;
            case HotkeyTarget.Jump7:
                Jump7 = "";
                _settings.Current.Hotkeys.Jump7 = "";
                break;
            case HotkeyTarget.Jump8:
                Jump8 = "";
                _settings.Current.Hotkeys.Jump8 = "";
                break;
            case HotkeyTarget.Jump9:
                Jump9 = "";
                _settings.Current.Hotkeys.Jump9 = "";
                break;
        }
        _settings.Save();
        _host.ApplyKvmOptions();
        ActiveListening = HotkeyTarget.None;
        ConflictMessage = null;
    }

    public bool CommitBinding(HotkeyBinding binding)
    {
        string text = binding.ToString();
        var target = ActiveListening;

        var allHotkeys = new (HotkeyTarget Target, string Name, string Value)[]
        {
            (HotkeyTarget.ReturnControl, "Return control", ReturnControl),
            (HotkeyTarget.LockCursor, "Lock cursor", LockCursor),
            (HotkeyTarget.QuickPaste, "Quick paste overlay", QuickPaste),
            (HotkeyTarget.PasteFromPeer, "Paste from other computer", PasteFromPeer),
            (HotkeyTarget.NextComputer, "Next computer", NextComputer),
            (HotkeyTarget.Jump1, "Jump to computer 1", Jump1),
            (HotkeyTarget.Jump2, "Jump to computer 2", Jump2),
            (HotkeyTarget.Jump3, "Jump to computer 3", Jump3),
            (HotkeyTarget.Jump4, "Jump to computer 4", Jump4),
            (HotkeyTarget.Jump5, "Jump to computer 5", Jump5),
            (HotkeyTarget.Jump6, "Jump to computer 6", Jump6),
            (HotkeyTarget.Jump7, "Jump to computer 7", Jump7),
            (HotkeyTarget.Jump8, "Jump to computer 8", Jump8),
            (HotkeyTarget.Jump9, "Jump to computer 9", Jump9),
        };

        foreach (var (otherTarget, otherName, otherValue) in allHotkeys)
        {
            if (otherTarget != target && HotkeyBinding.TryParse(otherValue, out var otherBinding) && otherBinding == binding)
            {
                ConflictMessage = $"Already used by {otherName}";
                return false;
            }
        }

        switch (target)
        {
            case HotkeyTarget.ReturnControl:
                ReturnControl = text;
                _settings.Current.Hotkeys.ReturnControl = text;
                break;
            case HotkeyTarget.LockCursor:
                LockCursor = text;
                _settings.Current.Hotkeys.LockCursor = text;
                break;
            case HotkeyTarget.QuickPaste:
                QuickPaste = text;
                _settings.Current.Hotkeys.QuickPaste = text;
                QuickPasteHotkeyChanged?.Invoke();
                break;
            case HotkeyTarget.PasteFromPeer:
                PasteFromPeer = text;
                _settings.Current.Hotkeys.PasteFromPeer = text;
                PasteFromPeerHotkeyChanged?.Invoke();
                break;
            case HotkeyTarget.NextComputer:
                NextComputer = text;
                _settings.Current.Hotkeys.NextComputer = text;
                break;
            case HotkeyTarget.Jump1:
                Jump1 = text;
                _settings.Current.Hotkeys.Jump1 = text;
                break;
            case HotkeyTarget.Jump2:
                Jump2 = text;
                _settings.Current.Hotkeys.Jump2 = text;
                break;
            case HotkeyTarget.Jump3:
                Jump3 = text;
                _settings.Current.Hotkeys.Jump3 = text;
                break;
            case HotkeyTarget.Jump4:
                Jump4 = text;
                _settings.Current.Hotkeys.Jump4 = text;
                break;
            case HotkeyTarget.Jump5:
                Jump5 = text;
                _settings.Current.Hotkeys.Jump5 = text;
                break;
            case HotkeyTarget.Jump6:
                Jump6 = text;
                _settings.Current.Hotkeys.Jump6 = text;
                break;
            case HotkeyTarget.Jump7:
                Jump7 = text;
                _settings.Current.Hotkeys.Jump7 = text;
                break;
            case HotkeyTarget.Jump8:
                Jump8 = text;
                _settings.Current.Hotkeys.Jump8 = text;
                break;
            case HotkeyTarget.Jump9:
                Jump9 = text;
                _settings.Current.Hotkeys.Jump9 = text;
                break;
        }

        _settings.Save();
        _host.ApplyKvmOptions();
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
