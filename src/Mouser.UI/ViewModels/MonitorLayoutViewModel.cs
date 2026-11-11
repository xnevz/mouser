using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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

    [ObservableProperty]
    private bool _showScreenLetters = true;

    [ObservableProperty]
    private bool _isDraggingAnyScreen;

    [ObservableProperty]
    private bool _isIdentifyingDisplays;

    public ObservableCollection<ScreenNodeViewModel> Screens { get; } = new();

    public MonitorLayoutViewModel()
    {
        // Default connected screen nodes for the visual canvas
        var macScreen = new ScreenNodeViewModel
        {
            Letter = "A",
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
            Letter = "B",
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
            Letter = "C",
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
    private void ToggleScreenLetters()
    {
        ShowScreenLetters = !ShowScreenLetters;
        StatusMessage = ShowScreenLetters
            ? "Screen identifier letters (A, B, C) enabled"
            : "Screen identifier letters hidden";
    }

    [RelayCommand]
    private async Task IdentifyDisplays()
    {
        IsIdentifyingDisplays = true;
        StatusMessage = "Identifying displays: Screen A (Mac), Screen B (Windows), Screen C (Linux)";
        await Task.Delay(2500);
        IsIdentifyingDisplays = false;
        StatusMessage = $"KVM Active — {Screens.Count} Screens Connected";
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
        StatusMessage = "Screens auto-arranged horizontally side-by-side";
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

    public void UpdateScreenPosition(ScreenNodeViewModel screen, double newX, double newY, bool enableSnapping = true)
    {
        const double canvasWidth = 1000;
        const double canvasHeight = 600;
        const double snapDistance = 16.0;

        double targetX = newX;
        double targetY = newY;

        if (enableSnapping)
        {
            double bestXDiff = snapDistance;
            double bestYDiff = snapDistance;
            double snappedX = targetX;
            double snappedY = targetY;

            foreach (var other in Screens)
            {
                if (other == screen) continue;

                // Horizontal docking: dock to right edge of other
                double diffRight = Math.Abs(targetX - (other.PositionX + other.DisplayWidth));
                if (diffRight < bestXDiff)
                {
                    bestXDiff = diffRight;
                    snappedX = other.PositionX + other.DisplayWidth;
                }

                // Horizontal docking: dock to left edge of other
                double diffLeft = Math.Abs((targetX + screen.DisplayWidth) - other.PositionX);
                if (diffLeft < bestXDiff)
                {
                    bestXDiff = diffLeft;
                    snappedX = other.PositionX - screen.DisplayWidth;
                }

                // Horizontal alignment: align left edges
                double diffAlignX = Math.Abs(targetX - other.PositionX);
                if (diffAlignX < bestXDiff)
                {
                    bestXDiff = diffAlignX;
                    snappedX = other.PositionX;
                }

                // Vertical alignment: align top edges
                double diffTop = Math.Abs(targetY - other.PositionY);
                if (diffTop < bestYDiff)
                {
                    bestYDiff = diffTop;
                    snappedY = other.PositionY;
                }

                // Vertical alignment: align bottom edges
                double diffBottom = Math.Abs((targetY + screen.DisplayHeight) - (other.PositionY + other.DisplayHeight));
                if (diffBottom < bestYDiff)
                {
                    bestYDiff = diffBottom;
                    snappedY = other.PositionY + other.DisplayHeight - screen.DisplayHeight;
                }

                // Vertical docking: dock to bottom edge of other
                double diffDockBottom = Math.Abs(targetY - (other.PositionY + other.DisplayHeight));
                if (diffDockBottom < bestYDiff)
                {
                    bestYDiff = diffDockBottom;
                    snappedY = other.PositionY + other.DisplayHeight;
                }

                // Vertical docking: dock to top edge of other
                double diffDockTop = Math.Abs((targetY + screen.DisplayHeight) - other.PositionY);
                if (diffDockTop < bestYDiff)
                {
                    bestYDiff = diffDockTop;
                    snappedY = other.PositionY - screen.DisplayHeight;
                }
            }

            targetX = snappedX;
            targetY = snappedY;
        }

        // Clamp inside canvas boundary
        double maxX = Math.Max(0, canvasWidth - screen.DisplayWidth);
        double maxY = Math.Max(0, canvasHeight - screen.DisplayHeight);

        screen.PositionX = Math.Clamp(Math.Round(targetX), 0, maxX);
        screen.PositionY = Math.Clamp(Math.Round(targetY), 0, maxY);

        StatusMessage = $"Screen {screen.Letter} ({screen.Hostname}) positioned at ({screen.PositionX:0}, {screen.PositionY:0})";
    }
}
