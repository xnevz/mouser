using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Mouser.Core.Kvm;
using Mouser.Core.Networking;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

public partial class DeviceDetailViewModel : ViewModelBase
{
    private readonly SettingsStore? _settings;
    private readonly MouserHost? _host;

    public string Id { get; }
    public DeviceKind Kind { get; }
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isOnline;
    [ObservableProperty] private string _ipAddress = string.Empty;
    [ObservableProperty] private string _operatingSystem = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private PeerPlacement _placement = PeerPlacement.None;
    [ObservableProperty] private bool _isPaired;
    [ObservableProperty] private string _pinText = string.Empty;

    [ObservableProperty] private bool _sendClipboard = true;
    [ObservableProperty] private bool _receiveClipboard = true;
    [ObservableProperty] private bool _sendFiles = true;

    [ObservableProperty] private double _mouseSpeed = 1.0;
    [ObservableProperty] private double _scrollSpeed = 1.0;
    [ObservableProperty] private bool _invertScroll;

    public bool IsComputer => Kind == DeviceKind.Computer;
    public bool IsPhone => Kind == DeviceKind.Phone;

    public DeviceDetailViewModel(string id, DeviceKind kind, SettingsStore? settings = null, MouserHost? host = null)
    {
        Id = id;
        Kind = kind;
        _settings = settings;
        _host = host;
        if (_settings?.Current.Peers.TryGetValue(id, out var s) == true)
        {
            _sendClipboard = s.SendClipboard;
            _receiveClipboard = s.ReceiveClipboard;
            _sendFiles = s.SendFiles;
            _mouseSpeed = s.MouseSpeed;
            _scrollSpeed = s.ScrollSpeed;
            _invertScroll = s.InvertScroll;
        }
    }

    partial void OnSendClipboardChanged(bool value)
    {
        if (_settings == null) return;
        var peer = GetOrCreatePeerSettings();
        if (peer.SendClipboard != value)
        {
            peer.SendClipboard = value;
            _settings.Save();
        }
    }

    partial void OnReceiveClipboardChanged(bool value)
    {
        if (_settings == null) return;
        var peer = GetOrCreatePeerSettings();
        if (peer.ReceiveClipboard != value)
        {
            peer.ReceiveClipboard = value;
            _settings.Save();
        }
    }

    partial void OnSendFilesChanged(bool value)
    {
        if (_settings == null) return;
        var peer = GetOrCreatePeerSettings();
        if (peer.SendFiles != value)
        {
            peer.SendFiles = value;
            _settings.Save();
        }
    }

    partial void OnMouseSpeedChanged(double value)
    {
        if (_settings == null) return;
        var peer = GetOrCreatePeerSettings();
        if (Math.Abs(peer.MouseSpeed - value) > 0.0001)
        {
            peer.MouseSpeed = value;
            _settings.Save();
            _host?.SetPeerMotion(Id, peer.MouseSpeed, peer.ScrollSpeed, peer.InvertScroll);
        }
    }

    partial void OnScrollSpeedChanged(double value)
    {
        if (_settings == null) return;
        var peer = GetOrCreatePeerSettings();
        if (Math.Abs(peer.ScrollSpeed - value) > 0.0001)
        {
            peer.ScrollSpeed = value;
            _settings.Save();
            _host?.SetPeerMotion(Id, peer.MouseSpeed, peer.ScrollSpeed, peer.InvertScroll);
        }
    }

    partial void OnInvertScrollChanged(bool value)
    {
        if (_settings == null) return;
        var peer = GetOrCreatePeerSettings();
        if (peer.InvertScroll != value)
        {
            peer.InvertScroll = value;
            _settings.Save();
            _host?.SetPeerMotion(Id, peer.MouseSpeed, peer.ScrollSpeed, peer.InvertScroll);
        }
    }

    private PeerSettings GetOrCreatePeerSettings()
    {
        if (!_settings!.Current.Peers.TryGetValue(Id, out var s))
        {
            s = new PeerSettings { Name = !string.IsNullOrWhiteSpace(Name) ? Name : Id };
            _settings.Current.Peers[Id] = s;
        }
        return s;
    }
}
