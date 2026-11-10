using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Mouser.UI.ViewModels;

public partial class MonitorLayoutViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isSharingActive = true;

    [ObservableProperty]
    private string _statusMessage = "KVM Active — 3 Screens Connected";

    [ObservableProperty]
    private ScreenNodeViewModel? _selectedScreen;

    public ObservableCollection<ScreenNodeViewModel> Screens { get; } = new();

    public MonitorLayoutViewModel()
    {
        // Default connected screen nodes for the visual canvas
        var macScreen = new ScreenNodeViewModel
        {
            Hostname = "MacBook Pro 16\"",
            OsType = "macOS",
            Resolution = "3456 × 2234 (Retina)",
            PositionX = 60,
            PositionY = 120,
            DisplayWidth = 260,
            DisplayHeight = 160,
            IsLocal = true,
            IsSelected = true,
            IpAddress = "192.168.1.101"
        };

        var winScreen = new ScreenNodeViewModel
        {
            Hostname = "Windows Workstation",
            OsType = "Windows 11",
            Resolution = "3840 × 2160 (4K UHD)",
            PositionX = 350,
            PositionY = 100,
            DisplayWidth = 280,
            DisplayHeight = 180,
            IsLocal = false,
            IpAddress = "192.168.1.102"
        };

        var linScreen = new ScreenNodeViewModel
        {
            Hostname = "Linux ThinkPad",
            OsType = "Linux (Wayland)",
            Resolution = "1920 × 1080 (FHD)",
            PositionX = 660,
            PositionY = 130,
            DisplayWidth = 230,
            DisplayHeight = 150,
            IsLocal = false,
            IpAddress = "192.168.1.103"
        };

        Screens.Add(macScreen);
        Screens.Add(winScreen);
        Screens.Add(linScreen);

        SelectedScreen = macScreen;
    }

    [RelayCommand]
    private void ToggleSharing()
    {
        IsSharingActive = !IsSharingActive;
        StatusMessage = IsSharingActive
            ? $"KVM Active — {Screens.Count} Screens Connected"
            : "KVM Paused — Input locked to local screen";
    }

    [RelayCommand]
    private void AutoArrangeScreens()
    {
        double currentX = 50;
        foreach (var screen in Screens)
        {
            screen.PositionX = currentX;
            screen.PositionY = 120;
            currentX += screen.DisplayWidth + 24;
        }
    }

    [RelayCommand]
    private void SelectScreen(ScreenNodeViewModel? screen)
    {
        if (screen == null) return;
        foreach (var s in Screens)
        {
            s.IsSelected = (s == screen);
        }
        SelectedScreen = screen;
    }
}
