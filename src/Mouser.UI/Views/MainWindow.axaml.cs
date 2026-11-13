using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Mouser.UI.ViewModels;

namespace Mouser.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.FocusSearchRequested += () =>
                {
                    var view = this.GetVisualDescendants().OfType<ClipboardHistoryView>().FirstOrDefault();
                    view?.FocusSearch();
                };
            }
        };
    }
}
