using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Geometry;
using Mouser.Core.Kvm;
using Mouser.Core.Networking;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

/// <summary>A physical monitor as reported by the OS, in desktop pixel coordinates.</summary>
public sealed record DisplayInfo(string Name, int X, int Y, int Width, int Height, double Scaling, bool IsPrimary);

/// <summary>Another Mouser computer on the network, with its pairing state and placement.</summary>
public partial class PeerViewModel : ViewModelBase
{
    private readonly MonitorLayoutViewModel _owner;
    private bool _updating;

    public DiscoveredPeer Peer { get; private set; }
    public string Id => Peer.DeviceId;
    public string Hostname => Peer.Hostname;
    public string OperatingSystem => Peer.OperatingSystem;
    public string Address => $"{Peer.IpAddress}:{Peer.MotionPort}";
    public string ScreenText => Peer.ScreenWidth > 0 ? $"{Peer.ScreenWidth} × {Peer.ScreenHeight}" : "screen size unknown";

    public static IReadOnlyList<PeerPlacement> PlacementOptions { get; } =
        new[] { PeerPlacement.Right, PeerPlacement.Left, PeerPlacement.Above, PeerPlacement.Below, PeerPlacement.None };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaired), nameof(StatusText), nameof(StatusColor), nameof(NeedsPin))]
    private PeerLinkStatus _status;

    [ObservableProperty] private PeerPlacement _placement;
    [ObservableProperty] private string _pinText = string.Empty;
    [ObservableProperty] private string? _error;

    public bool IsPaired => Status is PeerLinkStatus.Paired;
    public bool NeedsPin => Status is PeerLinkStatus.NotPaired;

    public string StatusText => Status switch
    {
        PeerLinkStatus.Paired => "Paired",
        PeerLinkStatus.Pairing => "Pairing…",
        PeerLinkStatus.Unreachable => "Not responding",
        _ => "Not paired"
    };

    public string StatusColor => Status switch
    {
        PeerLinkStatus.Paired => "#10b981",
        PeerLinkStatus.Unreachable => "#f59e0b",
        _ => "#64748b"
    };

    public PeerViewModel(MonitorLayoutViewModel owner, DiscoveredPeer peer, PeerLinkStatus status, PeerPlacement placement)
    {
        _owner = owner;
        Peer = peer;
        _status = status;
        _placement = placement;
    }

    internal void Update(DiscoveredPeer peer, PeerLinkStatus status, PeerPlacement placement)
    {
        Peer = peer;
        _updating = true;
        Status = status;
        Placement = placement;
        _updating = false;
        OnPropertyChanged(nameof(Address));
        OnPropertyChanged(nameof(ScreenText));
    }

    partial void OnPlacementChanged(PeerPlacement value)
    {
        if (!_updating) _owner.ChangePlacement(this, value);
    }

    [RelayCommand]
    private Task PairAsync() => _owner.PairAsync(this);

    [RelayCommand]
    private void Forget() => _owner.Forget(this);

    [RelayCommand]
    private Task SendFileAsync() => _owner.SendFileAsync(this);
}

/// <summary>
/// Shows this computer's monitors and the paired peers placed around it, and manages
/// desktop-to-desktop mouse and keyboard sharing.
/// </summary>
public partial class MonitorLayoutViewModel : ViewModelBase
{
    public const double CanvasWidth = 640;
    public const double CanvasHeight = 300;
    private const double CanvasPadding = 16;

    private readonly MouserHost? _host;
    private IReadOnlyList<DisplayInfo> _displays = Array.Empty<DisplayInfo>();
    private string? _layoutKey;

    [ObservableProperty] private ScreenNodeViewModel? _selectedScreen;
    [ObservableProperty] private string _desktopSummary = "No displays detected";
    [ObservableProperty] private string _kvmStatus = string.Empty;
    [ObservableProperty] private bool _isKvmEnabled;
    [ObservableProperty] private string? _transferStatus;

    /// <summary>Set by the app: shows a file picker and returns the chosen path.</summary>
    public Func<Task<string?>>? PickFile { get; set; }

    public ObservableCollection<ScreenNodeViewModel> Screens { get; } = new();
    public ObservableCollection<PeerViewModel> Peers { get; } = new();

    public bool HasPeers => Peers.Count > 0;
    public bool CanShareInput => _host?.CanShareInput ?? false;

    public MonitorLayoutViewModel(MouserHost? host = null, SettingsStore? settings = null)
    {
        _host = host;
        _isKvmEnabled = settings?.Current.KvmEnabled ?? false;
        Settings = settings;
    }

    private SettingsStore? Settings { get; }

    /// <summary>The bounding box of all local monitors, or null before the window knows them.</summary>
    public ScreenBounds? LocalDesktop => _displays.Count == 0 ? null : new ScreenBounds
    {
        X = _displays.Min(d => d.X),
        Y = _displays.Min(d => d.Y),
        Width = _displays.Max(d => d.X + d.Width) - _displays.Min(d => d.X),
        Height = _displays.Max(d => d.Y + d.Height) - _displays.Min(d => d.Y)
    };

    public void SetDisplays(IReadOnlyList<DisplayInfo> displays)
    {
        _displays = displays;
        if (LocalDesktop is { } desktop) _host?.SetLocalDesktop(desktop);

        DesktopSummary = displays.Count switch
        {
            0 => "No displays detected",
            1 => $"1 display • {LocalDesktop!.Width} × {LocalDesktop.Height}",
            _ => $"{displays.Count} displays • desktop spans {LocalDesktop!.Width} × {LocalDesktop.Height}"
        };
        Relayout();
    }

    /// <summary>Updates the peer list from discovery. Without a host (tests), peers are shown as not paired.</summary>
    public void SetPeers(IEnumerable<DiscoveredPeer> peers)
    {
        _host?.SyncPeers();
        var current = peers.OrderBy(p => p.Hostname, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var gone in Peers.Where(vm => current.All(p => p.DeviceId != vm.Id)).ToList()) Peers.Remove(gone);
        for (int i = 0; i < current.Count; i++)
        {
            var peer = current[i];
            var status = _host?.GetPeerStatus(peer.DeviceId) ?? PeerLinkStatus.NotPaired;
            var placement = SavedPlacement(peer.DeviceId);
            var vm = Peers.FirstOrDefault(p => p.Id == peer.DeviceId);
            if (vm == null) Peers.Insert(Math.Min(i, Peers.Count), new PeerViewModel(this, peer, status, placement));
            else vm.Update(peer, status, placement);
        }

        OnPropertyChanged(nameof(HasPeers));
        UpdateKvmStatus();
        Relayout();
    }

    private PeerPlacement SavedPlacement(string id) =>
        Settings?.Current.Peers.TryGetValue(id, out var s) == true ? s.Placement : PeerPlacement.None;

    internal async Task PairAsync(PeerViewModel peer)
    {
        if (_host == null) return;
        peer.Error = null;
        string digits = new(peer.PinText.Where(char.IsDigit).ToArray());
        if (digits.Length != 6)
        {
            peer.Error = "Enter the 6-digit PIN shown on that computer's Phone Remote page.";
            return;
        }

        peer.Status = PeerLinkStatus.Pairing;
        bool ok = await _host.PairPeerAsync(peer.Peer, int.Parse(digits));
        peer.Error = ok ? null : "That PIN was not accepted (or the computer didn't answer).";
        if (ok) peer.PinText = string.Empty;
        SetPeers(_host.Peers);
    }

    internal async Task SendFileAsync(PeerViewModel peer)
    {
        if (_host == null || PickFile == null) return;
        string? path = await PickFile();
        if (path == null) return;

        string name = System.IO.Path.GetFileName(path);
        var progress = new Progress<Mouser.Core.Files.FileTransferProgress>(
            p => TransferStatus = $"Sending {name} to {peer.Hostname}… {p.PercentComplete:0}%");
        try
        {
            await _host.SendFileAsync(peer.Peer, path, progress);
            TransferStatus = $"Sent {name} to {peer.Hostname}. It is saved in Downloads/Mouser there.";
        }
        catch (Exception e) when (e is System.IO.IOException or System.Net.Sockets.SocketException
                                       or InvalidOperationException or UnauthorizedAccessException)
        {
            TransferStatus = $"Couldn't send {name}: {e.Message}";
        }
    }

    internal void ChangePlacement(PeerViewModel peer, PeerPlacement placement)
    {
        _host?.SetPeerPlacement(peer.Peer, placement);
        Relayout();
        UpdateKvmStatus();
    }

    internal void Forget(PeerViewModel peer)
    {
        _host?.ForgetPeer(peer.Id);
        SetPeers(_host?.Peers ?? Peers.Select(p => p.Peer).ToList());
    }

    partial void OnIsKvmEnabledChanged(bool value)
    {
        _host?.SetKvmEnabled(value);
        UpdateKvmStatus();
    }

    public void UpdateKvmStatus()
    {
        var kvm = _host?.Kvm;
        KvmStatus = _host?.KvmError
            ?? (kvm == null ? "Sharing this computer's mouse and keyboard needs Windows or macOS." :
                kvm.ActivePeerName is { } name ? $"Controlling {name}. Move back through the edge or press Ctrl+Alt+Esc to return." :
                kvm.IsScreenLocked ? "Cursor locked to this computer. Press Scroll Lock or Ctrl+Alt+Esc to unlock." :
                !IsKvmEnabled ? "Sharing is off." :
                kvm.IsRunning ? "Move the cursor off the screen edge towards a paired computer to control it." :
                "Pair a computer below and choose which side of this screen it is on.");
    }

    [RelayCommand]
    private void SelectScreen(ScreenNodeViewModel? screen)
    {
        if (screen == null) return;
        foreach (var s in Screens) s.IsSelected = s == screen;
        SelectedScreen = screen;
    }

    /// <summary>Draws local monitors and placed peers to scale, as the KVM sees them.</summary>
    private void Relayout()
    {
        string key = string.Join(";", _displays) + "|" + string.Join(";", Peers.Select(p =>
            $"{p.Id},{p.IsPaired},{p.Placement},{p.Peer.ScreenWidth}x{p.Peer.ScreenHeight},{p.Hostname}"));
        if (key == _layoutKey) return; // refreshed every second; only redraw on change
        _layoutKey = key;

        string? selectedName = SelectedScreen?.Name;
        Screens.Clear();
        if (LocalDesktop is not { } local) { SelectedScreen = null; return; }

        var rects = _displays
            .OrderByDescending(d => d.IsPrimary).ThenBy(d => d.X).ThenBy(d => d.Y)
            .Select((d, i) => (Rect: new ScreenBounds { X = d.X, Y = d.Y, Width = d.Width, Height = d.Height }, Display: d, Peer: (PeerViewModel?)null, Number: i + 1))
            .ToList();

        foreach (var peer in Peers.Where(p => p.IsPaired && p.Placement != PeerPlacement.None && p.Peer.ScreenWidth > 0))
        {
            var r = new ScreenBounds { Width = peer.Peer.ScreenWidth, Height = peer.Peer.ScreenHeight };
            var placed = peer.Placement switch
            {
                PeerPlacement.Right => r with { X = local.Right, Y = local.Y },
                PeerPlacement.Left => r with { X = local.X - r.Width, Y = local.Y },
                PeerPlacement.Below => r with { X = local.X, Y = local.Bottom },
                _ => r with { X = local.X, Y = local.Y - r.Height }
            };
            rects.Add((placed, null!, peer, 0));
        }

        double minX = rects.Min(r => r.Rect.X), minY = rects.Min(r => r.Rect.Y);
        double maxX = rects.Max(r => r.Rect.Right), maxY = rects.Max(r => r.Rect.Bottom);
        double scale = Math.Min((CanvasWidth - 2 * CanvasPadding) / (maxX - minX), (CanvasHeight - 2 * CanvasPadding) / (maxY - minY));
        double offsetX = (CanvasWidth - (maxX - minX) * scale) / 2, offsetY = (CanvasHeight - (maxY - minY) * scale) / 2;

        foreach (var (rect, display, peer, number) in rects)
        {
            Screens.Add(new ScreenNodeViewModel
            {
                Number = number,
                IsPeer = peer != null,
                Name = peer?.Hostname ?? (string.IsNullOrWhiteSpace(display.Name) ? $"Display {number}" : display.Name),
                Resolution = $"{rect.Width} × {rect.Height}",
                ScalingText = peer != null ? "-" : $"{display.Scaling * 100:0}%",
                PositionText = peer != null ? $"{peer.Placement} of this computer" : $"{display.X}, {display.Y}",
                IsPrimary = display?.IsPrimary ?? false,
                PositionX = Math.Round(offsetX + (rect.X - minX) * scale),
                PositionY = Math.Round(offsetY + (rect.Y - minY) * scale),
                DisplayWidth = Math.Max(24, Math.Round(rect.Width * scale) - 4),
                DisplayHeight = Math.Max(16, Math.Round(rect.Height * scale) - 4)
            });
        }

        SelectScreen(Screens.FirstOrDefault(s => s.Name == selectedName) ?? Screens.FirstOrDefault());
    }
}
