using CommunityToolkit.Mvvm.ComponentModel;

namespace Mouser.UI.ViewModels;

public enum DeviceKind { Computer, Phone }

public partial class SidebarDeviceViewModel : ObservableObject
{
    public string Key { get; }            // device id (computers) or address (phones)
    public DeviceKind Kind { get; }
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isOnline;
    public Material.Icons.MaterialIconKind IconKind =>
        Kind == DeviceKind.Phone ? Material.Icons.MaterialIconKind.Cellphone : Material.Icons.MaterialIconKind.Monitor;
    public string StatusColor => IsOnline ? "#22c55e" : "#64748b";
    public string StatusText => IsOnline ? "Online" : "Offline";

    public SidebarDeviceViewModel(string key, DeviceKind kind) { Key = key; Kind = kind; }
    partial void OnIsOnlineChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(StatusText));
    }
}
