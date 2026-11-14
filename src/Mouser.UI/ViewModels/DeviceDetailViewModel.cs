using CommunityToolkit.Mvvm.ComponentModel;
using Mouser.Core.Kvm;
using Mouser.Core.Networking;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

public partial class DeviceDetailViewModel : ViewModelBase
{
    private readonly SettingsStore? _settings;

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

    public bool IsComputer => Kind == DeviceKind.Computer;
    public bool IsPhone => Kind == DeviceKind.Phone;

    public DeviceDetailViewModel(string id, DeviceKind kind, SettingsStore? settings = null)
    {
        Id = id;
        Kind = kind;
        _settings = settings;
        if (_settings?.Current.Peers.TryGetValue(id, out var s) == true)
        {
            _sendClipboard = s.SendClipboard;
            _receiveClipboard = s.ReceiveClipboard;
            _sendFiles = s.SendFiles;
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
