using CommunityToolkit.Mvvm.ComponentModel;

namespace Mouser.UI.ViewModels;

public partial class ScreenNodeViewModel : ViewModelBase
{
    public int Number { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Resolution { get; init; } = string.Empty;
    public string ScalingText { get; init; } = string.Empty;
    public string PositionText { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }

    public double PositionX { get; init; }
    public double PositionY { get; init; }
    public double DisplayWidth { get; init; }
    public double DisplayHeight { get; init; }

    [ObservableProperty] private bool _isSelected;

    public string RoleText => IsPrimary ? "Main display" : "Extended display";
}
