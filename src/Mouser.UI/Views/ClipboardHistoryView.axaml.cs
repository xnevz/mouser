using Avalonia.Controls;

namespace Mouser.UI.Views;

public partial class ClipboardHistoryView : UserControl
{
    public ClipboardHistoryView()
    {
        InitializeComponent();
    }

    public void FocusSearch()
    {
        var box = this.FindControl<TextBox>("SearchBox");
        box?.Focus();
        box?.SelectAll();
    }
}
