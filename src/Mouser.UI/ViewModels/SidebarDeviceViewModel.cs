using CommunityToolkit.Mvvm.ComponentModel;

namespace Mouser.UI.ViewModels;

public enum DeviceKind { Computer, Phone }

public partial class SidebarDeviceViewModel : ObservableObject
{
    public string Key { get; }            // device id (computers) or address (phones)
    public DeviceKind Kind { get; }
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isOnline;
    public string Icon => Kind == DeviceKind.Phone ? "📱" : "🖥";
    public string StatusColor => IsOnline ? "#10b981" : "#64748b";

    public SidebarDeviceViewModel(string key, DeviceKind kind) { Key = key; Kind = kind; }
    partial void OnIsOnlineChanged(bool value) => OnPropertyChanged(nameof(StatusColor));
}
