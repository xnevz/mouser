using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsStore _store;
    private readonly MouserHost _host;

    [ObservableProperty] private int _inputPort;
    [ObservableProperty] private bool _clipboardHistoryEnabled;
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

    public bool IsStartAtLoginSupported => StartupRegistration.IsSupported;
    public string[] ThemeOptions => ThemeService.Options;
    public string DataDirectory => _store.DataDirectory;

    public SettingsViewModel(SettingsStore store, MouserHost host)
    {
        _store = store;
        _host = host;
        var s = store.Current;
        _inputPort = s.InputPort;
        _clipboardHistoryEnabled = s.ClipboardHistoryEnabled;
        _skipSensitiveClips = s.SkipSensitiveClips;
        _clipboardSyncEnabled = s.ClipboardSyncEnabled;
        _clipboardRetentionDays = s.ClipboardRetentionDays;
        _startAtLogin = s.StartAtLogin;
        _closeToTray = s.CloseToTray;
        _minimizeToTray = s.MinimizeToTray;
        _notifications = s.Notifications;
        _theme = s.Theme;
        _deviceName = s.DeviceName;
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
    partial void OnSkipSensitiveClipsChanged(bool value) => Save(s => s.SkipSensitiveClips = value);
    partial void OnClipboardSyncEnabledChanged(bool value) => Save(s => s.ClipboardSyncEnabled = value);
    partial void OnCloseToTrayChanged(bool value) => Save(s => s.CloseToTray = value);
    partial void OnMinimizeToTrayChanged(bool value) => Save(s => s.MinimizeToTray = value);
    partial void OnNotificationsChanged(bool value) => Save(s => s.Notifications = value);

    partial void OnClipboardRetentionDaysChanged(int value)
    {
        if (value >= 1) Save(s => s.ClipboardRetentionDays = value);
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
