using CommunityToolkit.Mvvm.ComponentModel;

namespace Mouser.UI.ViewModels;

public partial class ScreenNodeViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _hostname = "Desktop";

    [ObservableProperty]
    private string _osType = "macOS";

    [ObservableProperty]
    private string _resolution = "1920 × 1080";

    [ObservableProperty]
    private double _positionX;

    [ObservableProperty]
    private double _positionY;

    [ObservableProperty]
    private double _displayWidth = 240;

    [ObservableProperty]
    private double _displayHeight = 150;

    [ObservableProperty]
    private bool _isLocal = true;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string _ipAddress = "127.0.0.1";

    public string BadgeColor => IsLocal ? "#3b82f6" : "#10b981";
    public string RoleText => IsLocal ? "This Mac (Host)" : "Remote Screen";
}
