using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
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
        SetUpChrome();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsDarkTheme = ActualThemeVariant == ThemeVariant.Dark;
                vm.FocusSearchRequested += () =>
                {
                    var view = this.GetVisualDescendants().OfType<ClipboardHistoryView>().FirstOrDefault();
                    view?.FocusSearch();
                };
            }
        };
        ActualThemeVariantChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.IsDarkTheme = ActualThemeVariant == ThemeVariant.Dark;
        };
    }

    // The title bar is our own (see MainWindow.axaml): the bar itself has the native TitleBar role (drag,
    // double-click to maximise, Aero Snap), while the caption buttons are ordinary buttons handled here.
    private void SetUpChrome()
    {
        MinimizeButton.Click += (_, _) => WindowState = WindowState.Minimized;
        MaximizeButton.Click += (_, _) => ToggleMaximized();
        CloseButton.Click += (_, _) => Close();

        if (OperatingSystem.IsMacOS())
        {
            // macOS keeps its native traffic lights on the left; make room for them and drop ours.
            CaptionButtons.IsVisible = false;
            HeaderBrand.Margin = new Thickness(78, 0, 12, 0);
        }

        // Windows and macOS resize from the native frame; only frameless platforms need our grips.
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsFreeBSD())
            ResizeGrips.IsVisible = false;
    }

    private void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
