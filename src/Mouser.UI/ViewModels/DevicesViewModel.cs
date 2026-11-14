using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Kvm;
using Mouser.Core.Networking;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

public partial class DevicesViewModel : ViewModelBase
{
    private readonly MouserHost _host;
    private readonly SettingsStore _settings;

    public MonitorLayoutViewModel Layout { get; }
    public AndroidRemoteViewModel Remote { get; }
    public string ThisDeviceName => _host.DeviceName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThisDeviceSelected), nameof(IsComputerSelected), nameof(IsPhoneSelected))]
    private object? _selected = "this";

    public bool IsThisDeviceSelected => Selected is string s && s == "this";
    public bool IsComputerSelected => Selected is PeerViewModel;
    public bool IsPhoneSelected => Selected is RemoteClientItem;

    [RelayCommand]
    private void Select(object? item) => Selected = item;

    public ObservableCollection<DeviceDetailViewModel> Devices { get; } = new();
    [ObservableProperty] private DeviceDetailViewModel? _selectedDevice;

    [ObservableProperty] private string _manualAddress = string.Empty;
    [ObservableProperty] private string? _manualError;

    [RelayCommand]
    private async Task AddByAddressAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualAddress)) return;
        ManualError = null;
        bool ok = await _host.AddManualPeerAsync(ManualAddress);
        if (ok)
        {
            ManualAddress = string.Empty;
            ManualError = "Added. It appears in the list within a few seconds.";
        }
        else
        {
            ManualError = "Couldn't find that address. Check the name or IP and that Mouser is running there.";
        }
    }

    public DevicesViewModel(MouserHost host, MonitorLayoutViewModel layout, AndroidRemoteViewModel remote, SettingsStore settings)
    {
        _host = host;
        Layout = layout;
        Remote = remote;
        _settings = settings;
        Refresh();
    }

    public DevicesViewModel(MouserHost host, SettingsStore settings, AndroidRemoteViewModel remote)
        : this(host, new MonitorLayoutViewModel(host, settings), remote, settings)
    {
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(ThisDeviceName));

        var currentIds = new HashSet<string>();

        // 1. Online computers from _host.Peers
        foreach (var peer in _host.Peers)
        {
            currentIds.Add(peer.DeviceId);
            var existing = Devices.FirstOrDefault(d => d.Id == peer.DeviceId && d.Kind == DeviceKind.Computer);
            bool isPaired = _settings.Current.Peers.TryGetValue(peer.DeviceId, out var saved) && saved.Pin != null;
            var placement = saved?.Placement ?? PeerPlacement.None;
            if (existing == null)
            {
                existing = new DeviceDetailViewModel(peer.DeviceId, DeviceKind.Computer, _settings, _host)
                {
                    Name = peer.Hostname,
                    IpAddress = peer.IpAddress.ToString(),
                    OperatingSystem = peer.OperatingSystem,
                    IsOnline = true,
                    IsPaired = isPaired,
                    Placement = placement,
                    StatusText = isPaired ? "Connected" : "Not paired"
                };
                Devices.Add(existing);
            }
            else
            {
                existing.Name = peer.Hostname;
                existing.IpAddress = peer.IpAddress.ToString();
                existing.OperatingSystem = peer.OperatingSystem;
                existing.IsOnline = true;
                existing.IsPaired = isPaired;
                existing.Placement = placement;
                existing.StatusText = isPaired ? "Connected" : "Not paired";
                if (saved != null)
                {
                    existing.SendClipboard = saved.SendClipboard;
                    existing.ReceiveClipboard = saved.ReceiveClipboard;
                    existing.SendFiles = saved.SendFiles;
                    existing.MouseSpeed = saved.MouseSpeed;
                    existing.ScrollSpeed = saved.ScrollSpeed;
                    existing.InvertScroll = saved.InvertScroll;
                }
            }
        }

        // 2. Offline saved computers from _settings.Current.Peers not in _host.Peers
        foreach (var (id, peerSettings) in _settings.Current.Peers)
        {
            if (!_host.Peers.Any(p => p.DeviceId == id))
            {
                currentIds.Add(id);
                var existing = Devices.FirstOrDefault(d => d.Id == id && d.Kind == DeviceKind.Computer);
                string name = string.IsNullOrEmpty(peerSettings.Name) ? id : peerSettings.Name;
                if (existing == null)
                {
                    existing = new DeviceDetailViewModel(id, DeviceKind.Computer, _settings, _host)
                    {
                        Name = name,
                        IsOnline = false,
                        IsPaired = peerSettings.Pin != null,
                        Placement = peerSettings.Placement,
                        StatusText = "Offline",
                        SendClipboard = peerSettings.SendClipboard,
                        ReceiveClipboard = peerSettings.ReceiveClipboard,
                        SendFiles = peerSettings.SendFiles,
                        MouseSpeed = peerSettings.MouseSpeed,
                        ScrollSpeed = peerSettings.ScrollSpeed,
                        InvertScroll = peerSettings.InvertScroll
                    };
                    Devices.Add(existing);
                }
                else
                {
                    existing.Name = name;
                    existing.IsOnline = false;
                    existing.IsPaired = peerSettings.Pin != null;
                    existing.Placement = peerSettings.Placement;
                    existing.StatusText = "Offline";
                    existing.SendClipboard = peerSettings.SendClipboard;
                    existing.ReceiveClipboard = peerSettings.ReceiveClipboard;
                    existing.SendFiles = peerSettings.SendFiles;
                    existing.MouseSpeed = peerSettings.MouseSpeed;
                    existing.ScrollSpeed = peerSettings.ScrollSpeed;
                    existing.InvertScroll = peerSettings.InvertScroll;
                }
            }
        }

        // 3. Phones from Remote.Clients
        foreach (var client in Remote.Clients)
        {
            currentIds.Add(client.Address);
            var existing = Devices.FirstOrDefault(d => d.Id == client.Address && d.Kind == DeviceKind.Phone);
            if (existing == null)
            {
                existing = new DeviceDetailViewModel(client.Address, DeviceKind.Phone)
                {
                    Name = client.Address,
                    IpAddress = client.Address,
                    IsOnline = client.IsActive,
                    IsPaired = true,
                    StatusText = client.IsActive ? "Connected" : "Idle"
                };
                Devices.Add(existing);
            }
            else
            {
                existing.Name = client.Address;
                existing.IpAddress = client.Address;
                existing.IsOnline = client.IsActive;
                existing.IsPaired = true;
                existing.StatusText = client.IsActive ? "Connected" : "Idle";
            }
        }

        // Remove devices that are no longer present
        for (int i = Devices.Count - 1; i >= 0; i--)
        {
            if (!currentIds.Contains(Devices[i].Id))
                Devices.RemoveAt(i);
        }

        if (SelectedDevice != null && !Devices.Contains(SelectedDevice))
            SelectedDevice = Devices.FirstOrDefault();
    }

    [RelayCommand]
    public async Task PairComputerAsync(DeviceDetailViewModel? d)
    {
        if (d == null || !d.IsComputer || string.IsNullOrWhiteSpace(d.PinText)) return;
        if (!int.TryParse(d.PinText, out int pin)) return;

        var peer = _host.Peers.FirstOrDefault(p => p.DeviceId == d.Id);
        if (peer != null)
        {
            await _host.PairPeerAsync(peer, pin);
        }
        else
        {
            // If offline or discovered, save pin directly
            if (!_settings.Current.Peers.TryGetValue(d.Id, out var saved))
                _settings.Current.Peers[d.Id] = saved = new PeerSettings { Name = d.Name };
            saved.Pin = pin;
            if (saved.Placement == PeerPlacement.None) saved.Placement = PeerPlacement.Right;
            _settings.Save();
            _host.SyncPeers();
        }
        Refresh();
    }

    [RelayCommand]
    public void ForgetDevice(DeviceDetailViewModel? d)
    {
        if (d == null) return;
        if (d.IsComputer)
        {
            _host.ForgetPeer(d.Id);
            Refresh();
        }
        else if (d.IsPhone)
        {
            var client = Remote.Clients.FirstOrDefault(c => c.Address == d.Id);
            if (client != null)
            {
                Remote.ForgetCommand.Execute(client);
                Refresh();
            }
        }
    }

    public void SetPlacement(DeviceDetailViewModel? d, PeerPlacement placement)
    {
        if (d == null || !d.IsComputer) return;
        d.Placement = placement;
        var peer = _host.Peers.FirstOrDefault(p => p.DeviceId == d.Id);
        if (peer != null)
        {
            _host.SetPeerPlacement(peer, placement);
        }
        else if (_settings.Current.Peers.TryGetValue(d.Id, out var saved))
        {
            saved.Placement = placement;
            _settings.Save();
            _host.SyncPeers();
        }
    }
}
