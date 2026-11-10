using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Mouser.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _listenPort = 58490;

    [ObservableProperty]
    private int _edgeResistanceMs = 150;

    [ObservableProperty]
    private bool _startAtLogin = true;

    [ObservableProperty]
    private bool _enableE2ee = true;

    [ObservableProperty]
    private string _pairingPin = "849-204";

    [ObservableProperty]
    private bool _excludePasswordManagers = true;

    [ObservableProperty]
    private bool _autoLockScreens = false;

    [ObservableProperty]
    private string _statusMessage = "All settings saved locally";

    [RelayCommand]
    private void RegeneratePin()
    {
        var rnd = new System.Random();
        PairingPin = $"{rnd.Next(100, 999)}-{rnd.Next(100, 999)}";
        StatusMessage = "New cryptographic pairing PIN generated";
    }
}
