using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Mouser.UI.ViewModels;

namespace Mouser.UI.Views;

public partial class MainWindow : Window
{
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        FitToWorkArea();
    }

    // The designed size (1140x740) is bigger than small screens; shrink to the working area (excludes the taskbar) and centre in it.
    private void FitToWorkArea()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;

        double scale = screen.Scaling;
        var area = screen.WorkingArea;
        var frame = FrameSize ?? ClientSize;
        double chromeW = frame.Width - ClientSize.Width, chromeH = frame.Height - ClientSize.Height;
        double maxW = area.Width / scale - chromeW, maxH = area.Height / scale - chromeH;

        MinWidth = Math.Min(MinWidth, maxW);
        MinHeight = Math.Min(MinHeight, maxH);
        Width = Math.Min(Width, maxW);
        Height = Math.Min(Height, maxH);

        int frameW = (int)((Width + chromeW) * scale), frameH = (int)((Height + chromeH) * scale);
        Position = new PixelPoint(area.X + (area.Width - frameW) / 2, area.Y + (area.Height - frameH) / 2);
    }

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
