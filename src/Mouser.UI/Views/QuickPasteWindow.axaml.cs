using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Mouser.UI.ViewModels;

namespace Mouser.UI.Views;

public partial class QuickPasteWindow : Window
{
    public QuickPasteWindow()
    {
        InitializeComponent();
        // Keyboard-first: arrows move the selection while typing keeps going to the search box.
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        Opened += (_, _) => this.FindControl<TextBox>("SearchBox")?.Focus();
        Deactivated += (_, _) => (DataContext as QuickPasteViewModel)?.CancelCommand.Execute(null);
        this.FindControl<ListBox>("ClipList")!.DoubleTapped += (_, _) => (DataContext as QuickPasteViewModel)?.PasteCommand.Execute(null);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (DataContext is not QuickPasteViewModel vm) return;
        switch (e.Key)
        {
            case Key.Escape: vm.CancelCommand.Execute(null); break;
            case Key.Enter: vm.PasteCommand.Execute(null); break;
            case Key.Down: vm.MoveSelection(1); break;
            case Key.Up: vm.MoveSelection(-1); break;
            default: return;
        }
        e.Handled = true;
    }
}
