using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Mouser.Core.Networking;

namespace Mouser.UI.ViewModels;

/// <summary>A physical monitor as reported by the OS, in desktop pixel coordinates.</summary>
public sealed record DisplayInfo(string Name, int X, int Y, int Width, int Height, double Scaling, bool IsPrimary);

public sealed record PeerItem(string Hostname, string OperatingSystem, string Address);

/// <summary>Shows this computer's real monitor layout and the Mouser computers found on the network.</summary>
public partial class MonitorLayoutViewModel : ViewModelBase
{
    public const double CanvasWidth = 640;
    public const double CanvasHeight = 300;
    private const double CanvasPadding = 16;

    [ObservableProperty] private ScreenNodeViewModel? _selectedScreen;
    [ObservableProperty] private string _desktopSummary = "No displays detected";

    public ObservableCollection<ScreenNodeViewModel> Screens { get; } = new();
    public ObservableCollection<PeerItem> Peers { get; } = new();

    public bool HasPeers => Peers.Count > 0;

    public void SetDisplays(IReadOnlyList<DisplayInfo> displays)
    {
        Screens.Clear();
        if (displays.Count == 0)
        {
            DesktopSummary = "No displays detected";
            SelectedScreen = null;
            return;
        }

        // Fit the whole virtual desktop into the canvas while keeping proportions.
        int minX = displays.Min(d => d.X), minY = displays.Min(d => d.Y);
        int maxX = displays.Max(d => d.X + d.Width), maxY = displays.Max(d => d.Y + d.Height);
        double scale = Math.Min((CanvasWidth - 2 * CanvasPadding) / (maxX - minX),
                                (CanvasHeight - 2 * CanvasPadding) / (maxY - minY));
        double offsetX = (CanvasWidth - (maxX - minX) * scale) / 2;
        double offsetY = (CanvasHeight - (maxY - minY) * scale) / 2;

        var ordered = displays.OrderByDescending(d => d.IsPrimary).ThenBy(d => d.X).ThenBy(d => d.Y).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            var d = ordered[i];
            Screens.Add(new ScreenNodeViewModel
            {
                Number = i + 1,
                Name = string.IsNullOrWhiteSpace(d.Name) ? $"Display {i + 1}" : d.Name,
                Resolution = $"{d.Width} × {d.Height}",
                ScalingText = $"{d.Scaling * 100:0}%",
                PositionText = $"{d.X}, {d.Y}",
                IsPrimary = d.IsPrimary,
                PositionX = Math.Round(offsetX + (d.X - minX) * scale),
                PositionY = Math.Round(offsetY + (d.Y - minY) * scale),
                DisplayWidth = Math.Max(24, Math.Round(d.Width * scale) - 4),
                DisplayHeight = Math.Max(16, Math.Round(d.Height * scale) - 4)
            });
        }

        DesktopSummary = displays.Count == 1
            ? $"1 display • {maxX - minX} × {maxY - minY}"
            : $"{displays.Count} displays • desktop spans {maxX - minX} × {maxY - minY}";
        SelectScreen(Screens[0]);
    }

    public void SetPeers(IEnumerable<DiscoveredPeer> peers)
    {
        var items = peers
            .OrderBy(p => p.Hostname, StringComparer.OrdinalIgnoreCase)
            .Select(p => new PeerItem(p.Hostname, p.OperatingSystem, $"{p.IpAddress}:{p.MotionPort}"))
            .ToList();

        if (items.SequenceEqual(Peers)) return;
        Peers.Clear();
        foreach (var item in items) Peers.Add(item);
        OnPropertyChanged(nameof(HasPeers));
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SelectScreen(ScreenNodeViewModel? screen)
    {
        if (screen == null) return;
        foreach (var s in Screens) s.IsSelected = s == screen;
        SelectedScreen = screen;
    }
}
