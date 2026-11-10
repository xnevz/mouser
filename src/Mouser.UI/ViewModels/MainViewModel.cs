using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Mouser.UI.ViewModels;

public enum NavigationTab
{
    DisplayMatrix,
    SmartClipboard,
    AndroidRemote,
    Settings
}

public partial class MainViewModel : ViewModelBase
{
    public MonitorLayoutViewModel MonitorLayout { get; } = new();
    public ClipboardHistoryViewModel ClipboardHistory { get; } = new();
    public AndroidRemoteViewModel AndroidRemote { get; } = new();
    public SettingsViewModel Settings { get; } = new();

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private NavigationTab _currentTab = NavigationTab.DisplayMatrix;

    [ObservableProperty]
    private string _systemStatusText = "3 Devices Connected • E2EE Active";

    [ObservableProperty]
    private bool _isOnline = true;

    [ObservableProperty]
    private int _connectedCount = 3;

    public MainViewModel()
    {
        _currentPage = MonitorLayout;
    }

    [RelayCommand]
    private void Navigate(string tabName)
    {
        switch (tabName)
        {
            case "DisplayMatrix":
                CurrentTab = NavigationTab.DisplayMatrix;
                CurrentPage = MonitorLayout;
                break;
            case "SmartClipboard":
                CurrentTab = NavigationTab.SmartClipboard;
                CurrentPage = ClipboardHistory;
                break;
            case "AndroidRemote":
                CurrentTab = NavigationTab.AndroidRemote;
                CurrentPage = AndroidRemote;
                break;
            case "Settings":
                CurrentTab = NavigationTab.Settings;
                CurrentPage = Settings;
                break;
        }
    }
}

