using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Networking;
using Mouser.Core.Services;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

public partial class OnboardingViewModel : ViewModelBase
{
    private readonly SettingsStore _settings;
    private readonly MouserHost? _host;
    private readonly Action? _onCompleted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1), nameof(IsStep2), nameof(IsStep3))]
    private int _currentStep = 1;

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

    [ObservableProperty] private string _deviceName;
    [ObservableProperty] private bool _clipboardSyncEnabled;
    [ObservableProperty] private bool _kvmEnabled;
    [ObservableProperty] private bool _acceptRemoteInput;

    [ObservableProperty] private int _discoveredDeviceCount;
    [ObservableProperty] private string _discoveredDevicesText = "No devices discovered yet";

    public OnboardingViewModel(SettingsStore settings, MouserHost? host = null, Action? onCompleted = null)
    {
        _settings = settings;
        _host = host;
        _onCompleted = onCompleted;

        _deviceName = string.IsNullOrWhiteSpace(_settings.Current.DeviceName)
            ? Environment.MachineName
            : _settings.Current.DeviceName;
        _clipboardSyncEnabled = _settings.Current.ClipboardSyncEnabled;
        _kvmEnabled = _settings.Current.KvmEnabled;
        _acceptRemoteInput = _settings.Current.AcceptRemoteInput;

        Refresh();
    }

    public void Refresh()
    {
        int count = (_host?.Peers?.Count ?? 0) + (_host?.Input?.Clients.Count ?? 0);
        DiscoveredDeviceCount = count;
        DiscoveredDevicesText = count switch
        {
            0 => "No devices discovered yet",
            1 => "1 device discovered nearby",
            _ => $"{count} devices discovered nearby"
        };
    }

    [RelayCommand]
    public void Next()
    {
        if (CurrentStep == 1)
        {
            SaveStep1();
            CurrentStep = 2;
        }
        else if (CurrentStep == 2)
        {
            SaveStep2();
            CurrentStep = 3;
        }
        else
        {
            Done();
        }
    }

    [RelayCommand]
    public void Done()
    {
        SaveStep1();
        SaveStep2();
        _settings.Current.FirstRunDone = true;
        _settings.Save();
        _onCompleted?.Invoke();
    }

    [RelayCommand]
    public void Skip()
    {
        SaveStep1();
        SaveStep2();
        _settings.Current.FirstRunDone = true;
        _settings.Save();
        _onCompleted?.Invoke();
    }

    private void SaveStep1()
    {
        var trimmed = DeviceName?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length <= 32)
        {
            _settings.Current.DeviceName = trimmed;
        }
        else if (string.IsNullOrWhiteSpace(trimmed))
        {
            _settings.Current.DeviceName = Environment.MachineName;
        }
        _host?.RestartDiscovery();
        _settings.Save();
    }

    private void SaveStep2()
    {
        bool acceptChanged = _settings.Current.AcceptRemoteInput != AcceptRemoteInput;
        _settings.Current.ClipboardSyncEnabled = ClipboardSyncEnabled;
        _settings.Current.KvmEnabled = KvmEnabled;
        _settings.Current.AcceptRemoteInput = AcceptRemoteInput;
        _settings.Save();
        if (acceptChanged)
        {
            _host?.Restart();
        }
    }
}
