using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Mouser.UI.ViewModels;

namespace Mouser.UI.Views;

public partial class ClipboardHistoryView : UserControl
{
    public ClipboardHistoryView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var clipList = this.FindControl<ListBox>("ClipList");
        clipList?.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is ClipboardHistoryViewModel vm)
        {
            var sv = e.Source as ScrollViewer ?? (sender as Visual)?.FindDescendantOfType<ScrollViewer>();
            if (sv != null)
            {
                double remaining = sv.Extent.Height - (sv.Offset.Y + sv.Viewport.Height);
                if (remaining < 300)
                {
                    if (vm.LoadMoreCommand.CanExecute(null))
                    {
                        vm.LoadMoreCommand.Execute(null);
                    }
                }
            }
        }
    }

    public void FocusSearch()
    {
        var box = this.FindControl<TextBox>("SearchBox");
        box?.Focus();
        box?.SelectAll();
    }
}
