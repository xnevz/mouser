using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Mouser.UI.ViewModels;

namespace Mouser.UI.Views;

public partial class HotkeysView : UserControl
{
    public HotkeysView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is HotkeysViewModel vm && vm.IsListening)
        {
            if (vm.HandleKey(e.Key, e.KeyModifiers))
            {
                e.Handled = true;
            }
        }
    }
}
