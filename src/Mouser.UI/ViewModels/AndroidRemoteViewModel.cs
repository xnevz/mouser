using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Mouser.UI.ViewModels;

public partial class AndroidRemoteViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isPhoneConnected = true;

    [ObservableProperty]
    private string _deviceModel = "Pixel 8 Pro (Android 14)";

    [ObservableProperty]
    private string _ipAddress = "192.168.1.155";

    [ObservableProperty]
    private string _batteryLevel = "88%";

    [ObservableProperty]
    private int _latencyMs = 2;

    [ObservableProperty]
    private double _trackpadSensitivity = 1.2;

    [ObservableProperty]
    private double _scrollSpeed = 1.0;

    [ObservableProperty]
    private bool _isGyroPointerActive = false;

    [ObservableProperty]
    private string _lastInputEvent = "Two-finger scroll detected (dy: -14)";

    [ObservableProperty]
    private string _statusMessage = "Zero-latency UDP channel connected";

    [RelayCommand]
    private void ToggleGyroPointer()
    {
        IsGyroPointerActive = !IsGyroPointerActive;
        StatusMessage = IsGyroPointerActive
            ? "3D Gyroscope Air Mouse active — hold phone to aim"
            : "Air mouse paused — trackpad mode active";
    }

    [RelayCommand]
    private void SendMediaAction(string action)
    {
        LastInputEvent = $"Media command dispatched: {action}";
    }
}
