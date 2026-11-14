using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Clipboard;
using Mouser.Core.Networking;
using Mouser.Core.Security;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

public enum NavigationTab
{
    AndroidRemote,
    SmartClipboard,
    DisplayMatrix,
    Settings,
    Activity,
    Snippets
}

public partial class MainViewModel : ViewModelBase
{
    private readonly MouserHost _host;
    private readonly SettingsStore _settings;
    private readonly ClipboardStorageService _clipboardStorage;
    private readonly AppLockService? _appLock;

    public bool IsLocked => _appLock?.IsLocked ?? false;
    public bool CanLockNow => _appLock is { IsConfigured: true, IsLocked: false };
    public LockViewModel? Lock { get; }

    public ObservableCollection<SidebarDeviceViewModel> Devices { get; } = new();
    public ObservableCollection<TagViewModel> Tags { get; } = new();
    [ObservableProperty] private int _onlineDeviceCount;

    public AndroidRemoteViewModel AndroidRemote { get; }
    public ClipboardHistoryViewModel ClipboardHistory { get; }
    public MonitorLayoutViewModel MonitorLayout { get; }
    public SettingsViewModel Settings { get; }
    public DevicesViewModel DevicesPage { get; }
    public ActivityViewModel Activity { get; }
    public SnippetsViewModel Snippets { get; }

    [ObservableProperty] private ViewModelBase _currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRemoteTab), nameof(IsClipboardTab), nameof(IsDisplaysTab), nameof(IsSettingsTab), nameof(IsActivityTab), nameof(IsSnippetsTab))]
    private NavigationTab _currentTab = NavigationTab.SmartClipboard;

    [ObservableProperty] private string _systemStatusTitle = string.Empty;
    [ObservableProperty] private string _systemStatusText = string.Empty;
    [ObservableProperty] private string _statusColor = "#64748b";
    [ObservableProperty] private bool _foregroundIsElevated;

    public Func<bool> ElevatedProbe { get; set; } = ForegroundWindowInfo.IsElevated;
    public Func<bool>? RemoteInputActiveProbe { get; set; }

    partial void OnForegroundIsElevatedChanged(bool value) => DevicesPage.ForegroundIsElevated = value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnboarding))]
    private OnboardingViewModel? _onboarding;

    public bool IsOnboarding => Onboarding != null;

    public bool IsRemoteTab => CurrentTab == NavigationTab.AndroidRemote;
    public bool IsClipboardTab => CurrentTab == NavigationTab.SmartClipboard;
    public bool IsDisplaysTab => CurrentTab == NavigationTab.DisplayMatrix;
    public bool IsSettingsTab => CurrentTab == NavigationTab.Settings;
    public bool IsActivityTab => CurrentTab == NavigationTab.Activity;
    public bool IsSnippetsTab => CurrentTab == NavigationTab.Snippets;

    public MainViewModel(MouserHost host, SettingsStore settings, ClipboardStorageService clipboardStorage, IClipboardAccess? clipboardAccess = null, AppLockService? appLock = null)
    {
        _host = host;
        _settings = settings;
        _clipboardStorage = clipboardStorage;
        _appLock = appLock;
        AndroidRemote = new AndroidRemoteViewModel(host, settings);
        ClipboardHistory = new ClipboardHistoryViewModel(clipboardStorage, settings);
        ClipboardHistory.TagsChanged += async () => await RefreshTagsAsync();
        Settings = new SettingsViewModel(settings, host, appLock, clipboardStorage);
        Settings.HistoryCleared += async () => await ClipboardHistory.ReloadAsync();
        MonitorLayout = new MonitorLayoutViewModel(host, settings);
        DevicesPage = new DevicesViewModel(host, MonitorLayout, AndroidRemote, settings);
        Activity = new ActivityViewModel(host.Log, host, clipboardAccess);
        Snippets = new SnippetsViewModel(clipboardStorage);
        ClipboardHistory.ShareCapturedText = host.ShareClipboardAsync;
        ClipboardHistory.SendClipToPeerHandler = (peer, text) => host.SendClipToAsync(peer, text);
        ClipboardHistory.SendFileToPeerAction = async filePath =>
        {
            var peer = host.Peers.FirstOrDefault(p => host.CanSendFileTo(p));
            if (peer != null)
            {
                await host.SendFileAsync(peer, filePath);
            }
        };
        _currentPage = ClipboardHistory;
        _currentTab = NavigationTab.SmartClipboard;

        if (_appLock != null)
        {
            Lock = new LockViewModel(_appLock);
            _appLock.Locked += () =>
            {
                OnPropertyChanged(nameof(IsLocked));
                OnPropertyChanged(nameof(CanLockNow));
                _host.SetLocked(true);
                ClipboardHistory.IsLocked = true;
            };
            _appLock.Unlocked += () =>
            {
                OnPropertyChanged(nameof(IsLocked));
                OnPropertyChanged(nameof(CanLockNow));
                _host.SetLocked(false);
                ClipboardHistory.IsLocked = false;
            };
            if (_appLock.IsLocked)
            {
                _host.SetLocked(true);
                ClipboardHistory.IsLocked = true;
            }
        }

        if (!_settings.Current.FirstRunDone)
        {
            Onboarding = new OnboardingViewModel(_settings, _host, () => Onboarding = null);
        }
        Refresh();
        _ = RefreshTagsAsync();
    }

    public void AttachClipboard(IClipboardAccess clipboard)
    {
        ClipboardHistory.AttachClipboard(clipboard);
        Activity.AttachClipboard(clipboard);
    }

    /// <summary>Pulls live state from the network services. Called once a second by the app.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(CanLockNow));
        Onboarding?.Refresh();
        AndroidRemote.Refresh();
        RefreshDevices();
        MonitorLayout.SetPeers(_host.Peers);
        ClipboardHistory.UpdateSendablePeers(_host.Peers);
        DevicesPage.Refresh();
        CheckElevatedForeground();

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

    private void CheckElevatedForeground()
    {
        bool isRemoteInputActive = RemoteInputActiveProbe?.Invoke() ?? (
            AndroidRemote.ActiveClientCount > 0 ||
            (_host.Input != null && _host.Input.Clients.Any(c => (DateTime.UtcNow - c.LastSeenUtc).TotalSeconds < 5))
        );

        if (isRemoteInputActive && _settings.Current.WarnElevatedWindows)
        {
            ForegroundIsElevated = ElevatedProbe();
        }
        else
        {
            ForegroundIsElevated = false;
        }
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
            "Activity" => (NavigationTab.Activity, Activity),
            "Snippets" => (NavigationTab.Snippets, Snippets),
            _ => (NavigationTab.DisplayMatrix, DevicesPage)
        };
    }

    [RelayCommand]
    private void ShowLibrary(string library)
    {
        Navigate("SmartClipboard");
        ClipboardHistory.ClearTag();
        ClipboardHistory.SetLibraryCommand.Execute(library);
    }

    [RelayCommand]
    private void FilterByTag(TagViewModel? tag)
    {
        Navigate("SmartClipboard");
        ClipboardHistory.SetTag(tag?.Name);
    }

    public async Task RefreshTagsAsync()
    {
        var tags = await _clipboardStorage.GetTagsAsync();
        Tags.Clear();
        foreach (var t in tags.Where(t => t.Count > 0))
        {
            Tags.Add(new TagViewModel(t));
        }
    }

    public event Action? FocusSearchRequested;
    [RelayCommand]
    private void FocusSearch()
    {
        if (CurrentTab != NavigationTab.SmartClipboard)
            Navigate("SmartClipboard");
        FocusSearchRequested?.Invoke();
    }

    [RelayCommand]
    private void LockNow()
    {
        if (_appLock is { IsConfigured: true })
        {
            _appLock.Lock();
        }
    }
}
