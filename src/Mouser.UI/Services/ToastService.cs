using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Mouser.UI.Views;

namespace Mouser.UI.Services;

public static class ToastService
{
    private static SettingsStore? _settings;
    private static readonly List<ToastWindow> _activeToasts = new();

    public static IReadOnlyList<ToastWindow> ActiveToasts => _activeToasts;

    public static void Initialize(SettingsStore settings) => _settings = settings;

    public static void Show(string title, string body)
    {
        if (Application.Current is not { } app) return;
        if (_settings?.Current.Notifications == false) return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Show(title, body));
            return;
        }

        var window = new ToastWindow();
        window.TitleBlock.Text = title;
        window.BodyBlock.Text = body;

        var screen = window.Screens.Primary ?? window.Screens.All.FirstOrDefault();
        if (screen != null)
        {
            double scale = screen.Scaling;
            int marginPx = (int)(16 * scale);
            int spacingPx = (int)(8 * scale);
            int widthPx = (int)(320 * scale);
            int defaultHeightPx = (int)(70 * scale);

            int stackOffset = 0;
            foreach (var existing in _activeToasts)
            {
                int h = existing.Bounds.Height > 0 ? (int)(existing.Bounds.Height * scale) : defaultHeightPx;
                stackOffset += h + spacingPx;
            }

            int targetX = screen.WorkingArea.X + screen.WorkingArea.Width - marginPx - widthPx;
            int targetY = screen.WorkingArea.Y + screen.WorkingArea.Height - marginPx - defaultHeightPx - stackOffset;
            window.Position = new PixelPoint(targetX, targetY);
        }

        _activeToasts.Add(window);
        window.Show();

        window.SizeChanged += (_, _) => RepositionToasts();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            window.Close();
            _activeToasts.Remove(window);
            RepositionToasts();
        };
        timer.Start();
    }

    public static void RepositionToasts()
    {
        if (_activeToasts.Count == 0) return;

        var sample = _activeToasts[0];
        var screen = sample.Screens.Primary ?? sample.Screens.All.FirstOrDefault();
        if (screen == null) return;

        double scale = screen.Scaling;
        int marginPx = (int)(16 * scale);
        int spacingPx = (int)(8 * scale);
        int widthPx = (int)(320 * scale);
        int defaultHeightPx = (int)(70 * scale);

        int currentBottom = screen.WorkingArea.Y + screen.WorkingArea.Height - marginPx;

        foreach (var toast in _activeToasts)
        {
            int heightPx = toast.Bounds.Height > 0 ? (int)(toast.Bounds.Height * scale) : defaultHeightPx;
            int targetX = screen.WorkingArea.X + screen.WorkingArea.Width - marginPx - widthPx;
            int targetY = currentBottom - heightPx;

            toast.Position = new PixelPoint(targetX, targetY);
            currentBottom = targetY - spacingPx;
        }
    }
}
