using CommunityToolkit.Mvvm.ComponentModel;

namespace Mouser.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Welcome to Avalonia!";
}
