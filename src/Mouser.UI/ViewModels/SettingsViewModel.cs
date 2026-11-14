using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Clipboard;
using Mouser.Core.Security;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

public sealed record RetentionPreset(string Label, int Hours);

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsStore _store;
    private readonly MouserHost _host;
    private readonly AppLockService? _appLock;
    private readonly ClipboardStorageService? _clipboardStorage;

    public static IReadOnlyList<RetentionPreset> RetentionOptions { get; } = new List<RetentionPreset>
    {
        new("1 hour", 1),
        new("24 hours", 24),
        new("7 days", 168),
        new("30 days", 720),
        new("1 year", 8760),
        new("Forever", -1)
    };

    [ObservableProperty] private RetentionPreset? _selectedRetention;
    [ObservableProperty] private int _inputPort;
    [ObservableProperty] private bool _acceptRemoteInput;
    [ObservableProperty] private bool _clipboardHistoryEnabled;
    [ObservableProperty] private bool _saveImages;
    [ObservableProperty] private bool _skipSensitiveClips;
    [ObservableProperty] private bool _clipboardSyncEnabled;
    [ObservableProperty] private int _clipboardRetentionDays;
    [ObservableProperty] private int _clipboardRetentionHours;
    [ObservableProperty] private bool _clearOnExit;
    [ObservableProperty] private string _panicWipeHotkey = string.Empty;
    [ObservableProperty] private string _historySizeText = "History uses 0 B";
    [ObservableProperty] private bool _startAtLogin;
    [ObservableProperty] private bool _runAsAdministrator;
    [ObservableProperty] private bool _showRestartElevatedPrompt;
    [ObservableProperty] private string? _restartElevatedMessage;

    public Func<bool> ElevatedProbe { get; set; } = ElevationInfo.IsCurrentProcessElevated;
    public bool IsElevated => ElevatedProbe();
    public bool CanRestartElevated => !IsElevated;
    public bool CanRestartNormal => IsElevated;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _notifications;
    [ObservableProperty] private bool _warnElevatedWindows;
    [ObservableProperty] private string _theme;
    [ObservableProperty] private string _deviceName;
    [ObservableProperty] private string _switchFeel = "Normal";
    [ObservableProperty] private bool _blockInFullscreen;
    [ObservableProperty] private bool _blockInMaximized;
    [ObservableProperty] private bool _blockInCorners;
    [ObservableProperty] private string _requireModifier = "None";
    [ObservableProperty] private bool _doubleTapOverride;
    [ObservableProperty] private bool _wrapAround;
    [ObservableProperty] private bool _proportionalEntry = true;
    [ObservableProperty] private bool _dimScreensOnControl = true;
    [ObservableProperty] private double _dimScreensOpacity = 0.4;

    public string[] ModifierOptions { get; } = ["None", "Ctrl", "Alt", "Shift"];

    [ObservableProperty] private string? _statusMessage;

    public event Func<Task>? HistoryCleared;

    // Security & App Lock properties
    [ObservableProperty] private bool _lockWithWindows;
    [ObservableProperty] private bool _allowRemoteLock;
    [ObservableProperty] private int _autoLockMinutes;

    [ObservableProperty] private bool _isSettingPassphrase;
    [ObservableProperty] private string _newPassphrase = "";
    [ObservableProperty] private string _confirmPassphrase = "";
    [ObservableProperty] private string? _setupError;
    [ObservableProperty] private string? _savedRecoveryKey;
    [ObservableProperty] private bool _showRecoveryKeyPrompt;

    [ObservableProperty] private bool _isChangingPassphrase;
    [ObservableProperty] private string _oldPassphrase = "";
    [ObservableProperty] private string _changeNewPassphrase = "";
    [ObservableProperty] private string _changeConfirmPassphrase = "";
    [ObservableProperty] private string? _changeError;

    [ObservableProperty] private bool _isRemovingLock;
    [ObservableProperty] private string _removePassphrase = "";
    [ObservableProperty] private string? _removeError;

    public bool IsLockConfigured => _appLock?.IsConfigured ?? false;
    public bool IsStartAtLoginSupported => StartupRegistration.IsSupported;
    public string[] ThemeOptions => ThemeService.Options;
    public string DataDirectory => _store.DataDirectory;
    public HotkeysViewModel Hotkeys { get; }

    public SettingsViewModel(SettingsStore store, MouserHost host, AppLockService? appLock = null, ClipboardStorageService? clipboardStorage = null)
    {
        _store = store;
        _host = host;
        _appLock = appLock;
        _clipboardStorage = clipboardStorage;
        Hotkeys = new HotkeysViewModel(store, host);
        var s = store.Current;
        _inputPort = s.InputPort;
        _acceptRemoteInput = s.AcceptRemoteInput;
        _clipboardHistoryEnabled = s.ClipboardHistoryEnabled;
        _saveImages = s.SaveImages;
        _skipSensitiveClips = s.SkipSensitiveClips;
        _clipboardSyncEnabled = s.ClipboardSyncEnabled;
        _clipboardRetentionDays = s.ClipboardRetentionDays;
        _clipboardRetentionHours = s.ClipboardRetentionHours;
        _clearOnExit = s.ClearOnExit;
        _panicWipeHotkey = s.PanicWipeHotkey ?? string.Empty;
        _selectedRetention = RetentionOptions.FirstOrDefault(r => r.Hours == s.ClipboardRetentionHours)
            ?? RetentionOptions.FirstOrDefault(r => r.Hours == 720)
            ?? RetentionOptions[3];
        _startAtLogin = s.StartAtLogin;
        _runAsAdministrator = s.RunAsAdministrator;
        _closeToTray = s.CloseToTray;
        _minimizeToTray = s.MinimizeToTray;
        _notifications = s.Notifications;
        _warnElevatedWindows = s.WarnElevatedWindows;
        _theme = s.Theme;
        _deviceName = s.DeviceName;
        _switchFeel = s.SwitchFeel;
        _blockInFullscreen = s.BlockInFullscreen;
        _blockInMaximized = s.BlockInMaximized;
        _blockInCorners = s.BlockInCorners;
        _requireModifier = s.RequireModifier;
        _doubleTapOverride = s.DoubleTapOverride;
        _wrapAround = s.WrapAround;
        _proportionalEntry = s.ProportionalEntry;
        _dimScreensOnControl = s.DimScreensOnControl;
        _dimScreensOpacity = s.DimScreensOpacity;
        _lockWithWindows = s.LockWithWindows;
        _allowRemoteLock = s.AllowRemoteLock;
        _autoLockMinutes = s.AutoLockMinutes;

        if (_appLock != null)
        {
            _appLock.Locked += () => OnPropertyChanged(nameof(IsLockConfigured));
            _appLock.Unlocked += () => OnPropertyChanged(nameof(IsLockConfigured));
        }

        _ = RefreshHistorySizeAsync();
    }

    partial void OnDeviceNameChanged(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is >= 1 and <= 32)
        {
            Save(s => s.DeviceName = trimmed);
            _host.RestartDiscovery();
        }
        else if (string.IsNullOrWhiteSpace(value))
        {
            Save(s => s.DeviceName = Environment.MachineName);
            _host.RestartDiscovery();
        }
    }

    partial void OnThemeChanged(string value)
    {
        ThemeService.Apply(value);
        Save(s => s.Theme = value);
    }

    partial void OnClipboardHistoryEnabledChanged(bool value) => Save(s => s.ClipboardHistoryEnabled = value);
    partial void OnSaveImagesChanged(bool value) => Save(s => s.SaveImages = value);
    partial void OnSkipSensitiveClipsChanged(bool value) => Save(s => s.SkipSensitiveClips = value);
    partial void OnClipboardSyncEnabledChanged(bool value) => Save(s => s.ClipboardSyncEnabled = value);
    partial void OnAcceptRemoteInputChanged(bool value)
    {
        Save(s => s.AcceptRemoteInput = value);
        _host.Restart();
    }
    partial void OnSwitchFeelChanged(string value)
    {
        _host?.SetSwitchFeel(value);
        OnPropertyChanged(nameof(IsSwitchFeelRelaxed));
        OnPropertyChanged(nameof(IsSwitchFeelNormal));
        OnPropertyChanged(nameof(IsSwitchFeelCareful));
    }

    public bool IsSwitchFeelRelaxed
    {
        get => SwitchFeel == "Relaxed";
        set { if (value) SwitchFeel = "Relaxed"; }
    }

    public bool IsSwitchFeelNormal
    {
        get => SwitchFeel == "Normal";
        set { if (value) SwitchFeel = "Normal"; }
    }

    public bool IsSwitchFeelCareful
    {
        get => SwitchFeel == "Careful";
        set { if (value) SwitchFeel = "Careful"; }
    }

    partial void OnBlockInFullscreenChanged(bool value)
    {
        Save(s => s.BlockInFullscreen = value);
        _host?.ApplyKvmOptions();
    }

    partial void OnBlockInMaximizedChanged(bool value)
    {
        Save(s => s.BlockInMaximized = value);
        _host?.ApplyKvmOptions();
    }

    partial void OnBlockInCornersChanged(bool value)
    {
        Save(s => s.BlockInCorners = value);
        _host?.ApplyKvmOptions();
    }

    partial void OnRequireModifierChanged(string value)
    {
        Save(s => s.RequireModifier = value);
        _host?.ApplyKvmOptions();
    }

    partial void OnDoubleTapOverrideChanged(bool value)
    {
        Save(s => s.DoubleTapOverride = value);
        _host?.ApplyKvmOptions();
    }

    partial void OnWrapAroundChanged(bool value)
    {
        Save(s => s.WrapAround = value);
        _host?.ApplyKvmOptions();
    }

    partial void OnProportionalEntryChanged(bool value)
    {
        Save(s => s.ProportionalEntry = value);
        _host?.ApplyKvmOptions();
    }

    public event Action? DimSettingsChanged;

    partial void OnDimScreensOnControlChanged(bool value)
    {
        Save(s => s.DimScreensOnControl = value);
        DimSettingsChanged?.Invoke();
    }

    partial void OnDimScreensOpacityChanged(double value)
    {
        var clamped = Math.Clamp(value, 0.0, 0.7);
        if (Math.Abs(clamped - value) > 0.0001)
        {
            _dimScreensOpacity = clamped;
        }
        Save(s => s.DimScreensOpacity = clamped);
        OnPropertyChanged(nameof(DimScreensOpacityPercentText));
        DimSettingsChanged?.Invoke();
    }

    public string DimScreensOpacityPercentText => $"{(int)Math.Round(DimScreensOpacity * 100)}%";
    partial void OnCloseToTrayChanged(bool value) => Save(s => s.CloseToTray = value);
    partial void OnMinimizeToTrayChanged(bool value) => Save(s => s.MinimizeToTray = value);
    partial void OnNotificationsChanged(bool value) => Save(s => s.Notifications = value);
    partial void OnWarnElevatedWindowsChanged(bool value) => Save(s => s.WarnElevatedWindows = value);
    partial void OnLockWithWindowsChanged(bool value) => Save(s => s.LockWithWindows = value);
    partial void OnAllowRemoteLockChanged(bool value) => Save(s => s.AllowRemoteLock = value);
    partial void OnAutoLockMinutesChanged(int value)
    {
        if (value >= 0) Save(s => s.AutoLockMinutes = value);
    }

    partial void OnClearOnExitChanged(bool value) => Save(s => s.ClearOnExit = value);
    partial void OnPanicWipeHotkeyChanged(string value) => Save(s => s.PanicWipeHotkey = value);

    partial void OnSelectedRetentionChanged(RetentionPreset? value)
    {
        if (value == null) return;
        ClipboardRetentionHours = value.Hours;
        Save(s =>
        {
            s.ClipboardRetentionHours = value.Hours;
            s.ClipboardRetentionDays = value.Hours > 0 ? Math.Max(1, value.Hours / 24) : 0;
        });
    }

    partial void OnClipboardRetentionDaysChanged(int value)
    {
        if (value >= 1)
        {
            ClipboardRetentionHours = value * 24;
            SelectedRetention = RetentionOptions.FirstOrDefault(r => r.Hours == ClipboardRetentionHours);
            Save(s =>
            {
                s.ClipboardRetentionDays = value;
                s.ClipboardRetentionHours = value * 24;
            });
        }
    }

    public string IgnoredAppsText
    {
        get => string.Join(Environment.NewLine, _store.Current.IgnoredApps ?? new());
        set
        {
            var list = (value ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToLowerInvariant())
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .ToList();
            _store.Current.IgnoredApps = list;
            _store.Save();
            OnPropertyChanged();
        }
    }

    partial void OnStartAtLoginChanged(bool value)
    {
        try
        {
            StartupRegistration.Apply(value, RunAsAdministrator);
            Save(s => s.StartAtLogin = value);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException)
        {
            StatusMessage = $"Couldn't change startup setting: {e.Message}";
        }
    }

    partial void OnRunAsAdministratorChanged(bool value)
    {
        Save(s => s.RunAsAdministrator = value);
        try
        {
            StartupRegistration.Apply(StartAtLogin, value);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException)
        {
            StatusMessage = $"Couldn't change startup setting: {e.Message}";
        }

        if (value && !IsElevated)
        {
            ShowRestartElevatedPrompt = true;
            RestartElevatedMessage = "Mouser needs to restart as administrator to apply this change.";
        }
        else if (!value)
        {
            ShowRestartElevatedPrompt = false;
        }
    }

    [RelayCommand]
    private void RequestRestartElevated()
    {
        if (IsElevated) return;
        ShowRestartElevatedPrompt = true;
        RestartElevatedMessage = "Mouser needs to restart as administrator to apply this change.";
    }

    [RelayCommand]
    private void RestartElevated()
    {
        bool restarted = ElevatedLaunch.RestartElevated();
        if (!restarted)
        {
            StatusMessage = "Windows permission was declined. Nothing changed.";
            RunAsAdministrator = false;
            ShowRestartElevatedPrompt = false;
        }
    }

    [RelayCommand]
    private void RestartNormal()
    {
        ElevatedLaunch.RestartNormal();
    }

    [RelayCommand]
    private void DismissRestartPrompt()
    {
        ShowRestartElevatedPrompt = false;
    }

    [RelayCommand]
    private void LockNow()
    {
        if (_appLock != null && _appLock.IsConfigured)
        {
            _appLock.Lock();
        }
    }

    [RelayCommand]
    private void StartSetupPassphrase()
    {
        IsSettingPassphrase = true;
        NewPassphrase = "";
        ConfirmPassphrase = "";
        SetupError = null;
        ShowRecoveryKeyPrompt = false;
        SavedRecoveryKey = null;
    }

    [RelayCommand]
    private void CancelSetupPassphrase()
    {
        IsSettingPassphrase = false;
        NewPassphrase = "";
        ConfirmPassphrase = "";
        SetupError = null;
        ShowRecoveryKeyPrompt = false;
        SavedRecoveryKey = null;
    }

    [RelayCommand]
    private void ConfirmSetupPassphrase()
    {
        if (_appLock == null) return;
        if (string.IsNullOrEmpty(NewPassphrase) || NewPassphrase.Length < 8)
        {
            SetupError = "Passphrase must be at least 8 characters.";
            return;
        }
        if (NewPassphrase != ConfirmPassphrase)
        {
            SetupError = "Passphrases do not match.";
            return;
        }

        try
        {
            string recoveryKey = _appLock.Setup(NewPassphrase);
            SavedRecoveryKey = recoveryKey;
            ShowRecoveryKeyPrompt = true;
            SetupError = null;
        }
        catch (Exception ex)
        {
            SetupError = ex.Message;
        }
    }

    [RelayCommand]
    private void AcknowledgeRecoveryKey()
    {
        ShowRecoveryKeyPrompt = false;
        SavedRecoveryKey = null;
        IsSettingPassphrase = false;
        NewPassphrase = "";
        ConfirmPassphrase = "";
        SetupError = null;
        OnPropertyChanged(nameof(IsLockConfigured));
        StatusMessage = "Passphrase configured.";
    }

    [RelayCommand]
    private void StartChangePassphrase()
    {
        IsChangingPassphrase = true;
        OldPassphrase = "";
        ChangeNewPassphrase = "";
        ChangeConfirmPassphrase = "";
        ChangeError = null;
    }

    [RelayCommand]
    private void CancelChangePassphrase()
    {
        IsChangingPassphrase = false;
        OldPassphrase = "";
        ChangeNewPassphrase = "";
        ChangeConfirmPassphrase = "";
        ChangeError = null;
    }

    [RelayCommand]
    private void ChangePassphrase()
    {
        if (_appLock == null) return;
        if (!IsChangingPassphrase && string.IsNullOrEmpty(OldPassphrase) && string.IsNullOrEmpty(ChangeNewPassphrase))
        {
            IsChangingPassphrase = true;
            ChangeError = null;
            return;
        }

        if (_appLock.BackoffRemaining > TimeSpan.Zero)
        {
            int sec = Math.Max(1, (int)Math.Ceiling(_appLock.BackoffRemaining.TotalSeconds));
            ChangeError = $"Too many tries. Wait {sec} seconds.";
            return;
        }
        if (string.IsNullOrEmpty(OldPassphrase))
        {
            ChangeError = "Enter your current passphrase.";
            return;
        }
        if (string.IsNullOrEmpty(ChangeNewPassphrase) || ChangeNewPassphrase.Length < 8)
        {
            ChangeError = "New passphrase must be at least 8 characters.";
            return;
        }
        if (ChangeNewPassphrase != ChangeConfirmPassphrase)
        {
            ChangeError = "New passphrases do not match.";
            return;
        }

        bool ok = _appLock.ChangePassphrase(OldPassphrase, ChangeNewPassphrase);
        if (!ok)
        {
            if (_appLock.BackoffRemaining > TimeSpan.Zero)
            {
                int sec = Math.Max(1, (int)Math.Ceiling(_appLock.BackoffRemaining.TotalSeconds));
                ChangeError = $"Too many tries. Wait {sec} seconds.";
            }
            else
            {
                ChangeError = "Incorrect current passphrase.";
            }
            return;
        }

        IsChangingPassphrase = false;
        OldPassphrase = "";
        ChangeNewPassphrase = "";
        ChangeConfirmPassphrase = "";
        ChangeError = null;
        StatusMessage = "Passphrase changed.";
    }

    [RelayCommand]
    private void StartRemoveLock()
    {
        IsRemovingLock = true;
        RemovePassphrase = "";
        RemoveError = null;
    }

    [RelayCommand]
    private void CancelRemoveLock()
    {
        IsRemovingLock = false;
        RemovePassphrase = "";
        RemoveError = null;
    }

    [RelayCommand]
    private void RemoveLock()
    {
        if (_appLock == null) return;
        if (!IsRemovingLock && string.IsNullOrEmpty(RemovePassphrase))
        {
            IsRemovingLock = true;
            RemoveError = null;
            return;
        }

        if (_appLock.BackoffRemaining > TimeSpan.Zero)
        {
            int sec = Math.Max(1, (int)Math.Ceiling(_appLock.BackoffRemaining.TotalSeconds));
            RemoveError = $"Too many tries. Wait {sec} seconds.";
            return;
        }
        if (string.IsNullOrEmpty(RemovePassphrase))
        {
            RemoveError = "Enter your current passphrase.";
            return;
        }

        try
        {
            _appLock.Remove(RemovePassphrase);
            IsRemovingLock = false;
            RemovePassphrase = "";
            RemoveError = null;
            OnPropertyChanged(nameof(IsLockConfigured));
            StatusMessage = "Lock removed.";
        }
        catch (UnauthorizedAccessException)
        {
            if (_appLock.BackoffRemaining > TimeSpan.Zero)
            {
                int sec = Math.Max(1, (int)Math.Ceiling(_appLock.BackoffRemaining.TotalSeconds));
                RemoveError = $"Too many tries. Wait {sec} seconds.";
            }
            else
            {
                RemoveError = "Incorrect passphrase.";
            }
        }
        catch (Exception ex)
        {
            RemoveError = ex.Message;
        }
    }

    [RelayCommand]
    public async Task RefreshHistorySizeAsync()
    {
        if (_clipboardStorage != null)
        {
            long bytes = await _clipboardStorage.GetDatabaseSizeBytesAsync();
            HistorySizeText = $"History uses {ClipboardItemViewModel.FormatFileSize(bytes)}";
        }
    }

    [RelayCommand]
    public async Task ClearAllHistoryAsync()
    {
        if (_clipboardStorage != null)
        {
            int removed = await _clipboardStorage.ClearAsync(includeFavorites: true);
            StatusMessage = $"Cleared all history ({removed} clips).";
            await RefreshHistorySizeAsync();
            if (HistoryCleared != null) await HistoryCleared.Invoke();
        }
    }

    [RelayCommand]
    public async Task OptimizeAsync()
    {
        if (_clipboardStorage != null)
        {
            await _clipboardStorage.OptimizeAsync();
            StatusMessage = "History database optimized.";
            await RefreshHistorySizeAsync();
        }
    }

    [RelayCommand]
    public async Task RebuildSearchAsync()
    {
        if (_clipboardStorage != null)
        {
            await _clipboardStorage.RebuildSearchIndexAsync();
            StatusMessage = "Search index rebuilt.";
        }
    }

    [RelayCommand]
    public async Task PanicWipeAsync()
    {
        if (_clipboardStorage != null)
        {
            int count = await _clipboardStorage.ClearAsync(includeFavorites: true);
            StatusMessage = "All clipboard history and media wiped.";
            await RefreshHistorySizeAsync();
            if (HistoryCleared != null) await HistoryCleared.Invoke();
        }
    }

    [RelayCommand]
    private void ApplyPort()
    {
        if (InputPort is < 1024 or > 65535)
        {
            StatusMessage = "Choose a port between 1024 and 65535.";
            return;
        }

        Save(s => s.InputPort = InputPort);
        _host.Restart();
        StatusMessage = _host.InputError ?? $"Now listening on port {InputPort}. Reconnect your phone.";
    }

    private void Save(Action<AppSettings> change)
    {
        change(_store.Current);
        _store.Save();
        StatusMessage = "Saved.";
    }
}
