using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Security;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsStore _store;
    private readonly MouserHost _host;
    private readonly AppLockService? _appLock;

    [ObservableProperty] private int _inputPort;
    [ObservableProperty] private bool _acceptRemoteInput;
    [ObservableProperty] private bool _clipboardHistoryEnabled;
    [ObservableProperty] private bool _saveImages;
    [ObservableProperty] private bool _skipSensitiveClips;
    [ObservableProperty] private bool _clipboardSyncEnabled;
    [ObservableProperty] private int _clipboardRetentionDays;
    [ObservableProperty] private bool _startAtLogin;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _notifications;
    [ObservableProperty] private string _theme;
    [ObservableProperty] private string _deviceName;
    [ObservableProperty] private string? _statusMessage;

    // Security & App Lock properties
    [ObservableProperty] private bool _lockWithWindows;
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

    public SettingsViewModel(SettingsStore store, MouserHost host, AppLockService? appLock = null)
    {
        _store = store;
        _host = host;
        _appLock = appLock;
        var s = store.Current;
        _inputPort = s.InputPort;
        _acceptRemoteInput = s.AcceptRemoteInput;
        _clipboardHistoryEnabled = s.ClipboardHistoryEnabled;
        _saveImages = s.SaveImages;
        _skipSensitiveClips = s.SkipSensitiveClips;
        _clipboardSyncEnabled = s.ClipboardSyncEnabled;
        _clipboardRetentionDays = s.ClipboardRetentionDays;
        _startAtLogin = s.StartAtLogin;
        _closeToTray = s.CloseToTray;
        _minimizeToTray = s.MinimizeToTray;
        _notifications = s.Notifications;
        _theme = s.Theme;
        _deviceName = s.DeviceName;
        _lockWithWindows = s.LockWithWindows;
        _autoLockMinutes = s.AutoLockMinutes;

        if (_appLock != null)
        {
            _appLock.Locked += () => OnPropertyChanged(nameof(IsLockConfigured));
            _appLock.Unlocked += () => OnPropertyChanged(nameof(IsLockConfigured));
        }
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
    partial void OnCloseToTrayChanged(bool value) => Save(s => s.CloseToTray = value);
    partial void OnMinimizeToTrayChanged(bool value) => Save(s => s.MinimizeToTray = value);
    partial void OnNotificationsChanged(bool value) => Save(s => s.Notifications = value);
    partial void OnLockWithWindowsChanged(bool value) => Save(s => s.LockWithWindows = value);
    partial void OnAutoLockMinutesChanged(int value)
    {
        if (value >= 0) Save(s => s.AutoLockMinutes = value);
    }

    partial void OnClipboardRetentionDaysChanged(int value)
    {
        if (value >= 1) Save(s => s.ClipboardRetentionDays = value);
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
            StartupRegistration.Apply(value);
            Save(s => s.StartAtLogin = value);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException)
        {
            StatusMessage = $"Couldn't change startup setting: {e.Message}";
        }
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
