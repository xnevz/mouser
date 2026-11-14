using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Clipboard;
using Mouser.Core.Networking;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

public enum NavigationTab
{
    AndroidRemote,
    SmartClipboard,
    DisplayMatrix,
    Settings
}

public partial class MainViewModel : ViewModelBase
{
    private readonly MouserHost _host;
    private readonly SettingsStore _settings;

    public ObservableCollection<SidebarDeviceViewModel> Devices { get; } = new();
    [ObservableProperty] private int _onlineDeviceCount;

    public AndroidRemoteViewModel AndroidRemote { get; }
    public ClipboardHistoryViewModel ClipboardHistory { get; }
    public MonitorLayoutViewModel MonitorLayout { get; }
    public SettingsViewModel Settings { get; }
    public DevicesViewModel DevicesPage { get; }

    [ObservableProperty] private ViewModelBase _currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRemoteTab), nameof(IsClipboardTab), nameof(IsDisplaysTab), nameof(IsSettingsTab))]
    private NavigationTab _currentTab = NavigationTab.SmartClipboard;

    [ObservableProperty] private string _systemStatusTitle = string.Empty;
    [ObservableProperty] private string _systemStatusText = string.Empty;
    [ObservableProperty] private string _statusColor = "#64748b";

    public bool IsRemoteTab => CurrentTab == NavigationTab.AndroidRemote;
    public bool IsClipboardTab => CurrentTab == NavigationTab.SmartClipboard;
    public bool IsDisplaysTab => CurrentTab == NavigationTab.DisplayMatrix;
    public bool IsSettingsTab => CurrentTab == NavigationTab.Settings;

    public MainViewModel(MouserHost host, SettingsStore settings, ClipboardStorageService clipboardStorage)
    {
        _host = host;
        _settings = settings;
        AndroidRemote = new AndroidRemoteViewModel(host, settings);
        ClipboardHistory = new ClipboardHistoryViewModel(clipboardStorage, settings);
        Settings = new SettingsViewModel(settings, host);
        MonitorLayout = new MonitorLayoutViewModel(host, settings);
        DevicesPage = new DevicesViewModel(host, MonitorLayout, AndroidRemote, settings);
        ClipboardHistory.ShareCapturedText = host.ShareClipboardAsync;
        _currentPage = ClipboardHistory;
        _currentTab = NavigationTab.SmartClipboard;
        Refresh();
    }

    /// <summary>Pulls live state from the network services. Called once a second by the app.</summary>
    public void Refresh()
    {
        AndroidRemote.Refresh();
        RefreshDevices();
        MonitorLayout.SetPeers(_host.Peers);
        DevicesPage.Refresh();

        if (_host.Kvm?.ActivePeerName is { } controlled)
        {
            SystemStatusTitle = $"Controlling {controlled}";
            SystemStatusText = "Ctrl+Alt+Esc to come back";
            StatusColor = "#f59e0b";
            return;
        }

        if (_host.Input == null)
        {
            SystemStatusTitle = _host.InputError != null ? "Not listening" : "Remote input off";
            SystemStatusText = _host.InputError ?? "Turn it on in Phone Remote";
            StatusColor = _host.InputError != null ? "#f43f5e" : "#64748b";
            return;
        }

        int active = AndroidRemote.ActiveClientCount;
        SystemStatusTitle = active switch
        {
            0 => "Ready for phones",
            1 => "1 device connected",
            _ => $"{active} devices connected"
        };
        var ip = NetworkInfo.GetLocalIPv4Addresses().FirstOrDefault();
        SystemStatusText = ip == null ? "No network connection" : $"{ip}:{_host.Input.BoundPort}";
        StatusColor = active > 0 ? "#10b981" : "#6366f1";
    }

    private void RefreshDevices()
    {
        var currentKeys = new HashSet<string>();

        // Computers online: every _host.Peers item -> Key = DeviceId, Name = Hostname, IsOnline = true, Kind = Computer
        foreach (var peer in _host.Peers)
        {
            currentKeys.Add(peer.DeviceId);
            var existing = Devices.FirstOrDefault(d => d.Key == peer.DeviceId && d.Kind == DeviceKind.Computer);
            if (existing == null)
            {
                existing = new SidebarDeviceViewModel(peer.DeviceId, DeviceKind.Computer)
                {
                    Name = peer.Hostname,
                    IsOnline = true
                };
                Devices.Add(existing);
            }
            else
            {
                existing.Name = peer.Hostname;
                existing.IsOnline = true;
            }
        }

        // Computers offline: every _settings.Current.Peers entry whose id is not in _host.Peers
        foreach (var (id, peerSettings) in _settings.Current.Peers)
        {
            if (!_host.Peers.Any(p => p.DeviceId == id))
            {
                currentKeys.Add(id);
                var existing = Devices.FirstOrDefault(d => d.Key == id && d.Kind == DeviceKind.Computer);
                string name = string.IsNullOrEmpty(peerSettings.Name) ? id : peerSettings.Name;
                if (existing == null)
                {
                    existing = new SidebarDeviceViewModel(id, DeviceKind.Computer)
                    {
                        Name = name,
                        IsOnline = false
                    };
                    Devices.Add(existing);
                }
                else
                {
                    existing.Name = name;
                    existing.IsOnline = false;
                }
            }
        }

        // Phones: every AndroidRemote.Clients item -> Key = Address, Name = Address, Kind = Phone, IsOnline = IsActive
        foreach (var client in AndroidRemote.Clients)
        {
            currentKeys.Add(client.Address);
            var existing = Devices.FirstOrDefault(d => d.Key == client.Address && d.Kind == DeviceKind.Phone);
            if (existing == null)
            {
                existing = new SidebarDeviceViewModel(client.Address, DeviceKind.Phone)
                {
                    Name = client.Address,
                    IsOnline = client.IsActive
                };
                Devices.Add(existing);
            }
            else
            {
                existing.Name = client.Address;
                existing.IsOnline = client.IsActive;
            }
        }

        // Remove devices that are no longer present
        for (int i = Devices.Count - 1; i >= 0; i--)
        {
            if (!currentKeys.Contains(Devices[i].Key))
            {
                Devices.RemoveAt(i);
            }
        }

        OnlineDeviceCount = Devices.Count(d => d.IsOnline);
    }

    [RelayCommand]
    private void Navigate(string tabName)
    {
        if (tabName == "AndroidRemote")
        {
            DevicesPage.SelectCommand.Execute("this");
        }

        (CurrentTab, CurrentPage) = tabName switch
        {
            "SmartClipboard" => (NavigationTab.SmartClipboard, (ViewModelBase)ClipboardHistory),
            "DisplayMatrix" or "AndroidRemote" => (NavigationTab.DisplayMatrix, DevicesPage),
            "Settings" => (NavigationTab.Settings, Settings),
            _ => (NavigationTab.DisplayMatrix, DevicesPage)
        };
    }

    [RelayCommand]
    private void ShowLibrary(string library)
    {
        Navigate("SmartClipboard");
        ClipboardHistory.SetLibraryCommand.Execute(library);
    }

    public event Action? FocusSearchRequested;
    [RelayCommand]
    private void FocusSearch()
    {
        if (CurrentTab != NavigationTab.SmartClipboard)
            Navigate("SmartClipboard");
        FocusSearchRequested?.Invoke();
    }
}
