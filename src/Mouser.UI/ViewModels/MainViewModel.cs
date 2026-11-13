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

    public AndroidRemoteViewModel AndroidRemote { get; }
    public ClipboardHistoryViewModel ClipboardHistory { get; }
    public MonitorLayoutViewModel MonitorLayout { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty] private ViewModelBase _currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRemoteTab), nameof(IsClipboardTab), nameof(IsDisplaysTab), nameof(IsSettingsTab))]
    private NavigationTab _currentTab = NavigationTab.AndroidRemote;

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
        AndroidRemote = new AndroidRemoteViewModel(host, settings);
        ClipboardHistory = new ClipboardHistoryViewModel(clipboardStorage, settings);
        Settings = new SettingsViewModel(settings, host);
        MonitorLayout = new MonitorLayoutViewModel(host, settings);
        ClipboardHistory.ShareCapturedText = host.ShareClipboardAsync;
        _currentPage = AndroidRemote;
        Refresh();
    }

    /// <summary>Pulls live state from the network services. Called once a second by the app.</summary>
    public void Refresh()
    {
        AndroidRemote.Refresh();
        MonitorLayout.SetPeers(_host.Peers);

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

    [RelayCommand]
    private void Navigate(string tabName)
    {
        (CurrentTab, CurrentPage) = tabName switch
        {
            "SmartClipboard" => (NavigationTab.SmartClipboard, (ViewModelBase)ClipboardHistory),
            "DisplayMatrix" => (NavigationTab.DisplayMatrix, MonitorLayout),
            "Settings" => (NavigationTab.Settings, Settings),
            _ => (NavigationTab.AndroidRemote, AndroidRemote)
        };
    }
}
