using CommunityToolkit.Mvvm.ComponentModel;
using Mouser.Core.Kvm;
using Mouser.Core.Networking;

namespace Mouser.UI.ViewModels;

public partial class DeviceDetailViewModel : ViewModelBase
{
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

    public bool IsComputer => Kind == DeviceKind.Computer;
    public bool IsPhone => Kind == DeviceKind.Phone;

    public DeviceDetailViewModel(string id, DeviceKind kind)
    {
        Id = id;
        Kind = kind;
    }
}
