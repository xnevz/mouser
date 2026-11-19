using System;
using System.Runtime.InteropServices;
using Avalonia;
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
        PositionNextToCursor();
        // Keyboard-first: arrows move the selection while typing keeps going to the search box.
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        Opened += (_, _) =>
        {
            Services.WindowChrome.ApplyRoundedPopup(this);
            PositionNextToCursor();
            this.FindControl<TextBox>("SearchBox")?.Focus();
        };
        Deactivated += (_, _) => (DataContext as QuickPasteViewModel)?.CancelCommand.Execute(null);
        this.FindControl<ListBox>("ClipList")!.DoubleTapped += (_, _) => (DataContext as QuickPasteViewModel)?.PasteCommand.Execute(null);
    }

    public void PositionNextToCursor()
    {
        if (!OperatingSystem.IsWindows())
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        if (!GetCursorPos(out var pt))
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        var cursorPoint = new PixelPoint(pt.X, pt.Y);
        var screen = Screens.ScreenFromPoint(cursorPoint) ?? Screens.Primary ?? (Screens.All.Count > 0 ? Screens.All[0] : null);
        if (screen == null) return;

        double scale = screen.Scaling;
        double w = Bounds.Width > 0 ? Bounds.Width : (Width > 0 ? Width : 560);
        double h = Bounds.Height > 0 ? Bounds.Height : 400;

        int widthPx = (int)(w * scale);
        int heightPx = (int)(h * scale);

        int offsetX = (int)(12 * scale);
        int offsetY = (int)(12 * scale);
        int targetX = cursorPoint.X + offsetX;
        int targetY = cursorPoint.Y + offsetY;

        var area = screen.WorkingArea;
        int minX = area.X;
        int maxX = Math.Max(minX, area.X + area.Width - widthPx);
        int minY = area.Y;
        int maxY = Math.Max(minY, area.Y + area.Height - heightPx);

        int clampedX = Math.Clamp(targetX, minX, maxX);
        int clampedY = Math.Clamp(targetY, minY, maxY);

        Position = new PixelPoint(clampedX, clampedY);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (DataContext is not QuickPasteViewModel vm) return;

        // Pressing a digit 1-9 pastes that item when search box is empty
        if (string.IsNullOrEmpty(vm.Query) && e.KeyModifiers == KeyModifiers.None)
        {
            int digit = e.Key switch
            {
                Key.D1 or Key.NumPad1 => 1,
                Key.D2 or Key.NumPad2 => 2,
                Key.D3 or Key.NumPad3 => 3,
                Key.D4 or Key.NumPad4 => 4,
                Key.D5 or Key.NumPad5 => 5,
                Key.D6 or Key.NumPad6 => 6,
                Key.D7 or Key.NumPad7 => 7,
                Key.D8 or Key.NumPad8 => 8,
                Key.D9 or Key.NumPad9 => 9,
                _ => 0
            };

            if (digit >= 1 && digit <= 9)
            {
                if (vm.PasteDigit(digit, rich: false))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        switch (e.Key)
        {
            case Key.Escape: vm.CancelCommand.Execute(null); break;
            case Key.Enter:
                bool rich = (e.KeyModifiers & KeyModifiers.Shift) != 0;
                vm.PasteSelected(rich);
                break;
            case Key.Down: vm.MoveSelection(1); break;
            case Key.Up: vm.MoveSelection(-1); break;
            case Key.Tab:
                bool reverse = (e.KeyModifiers & KeyModifiers.Shift) != 0;
                vm.CycleTransform(reverse ? -1 : 1);
                break;
            default: return;
        }
        e.Handled = true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);
}
