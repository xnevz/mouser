using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Mouser.UI.Views;

namespace Mouser.UI.Services;

public interface IDimOverlayWindow
{
    PixelPoint Position { get; set; }
    double Width { get; set; }
    double Height { get; set; }
    void SetPeerName(string name);
    void SetDimOpacity(double opacity);
    void Show();
    void Close();
}

public readonly record struct OverlayScreen(PixelRect Bounds, double Scaling);

public sealed class DimOverlayService : IDisposable
{
    private readonly SettingsStore _settings;
    private readonly Func<IReadOnlyList<OverlayScreen>> _screenProvider;
    private readonly Func<IDimOverlayWindow> _windowFactory;
    private readonly Action<IDimOverlayWindow>? _onWindowShown;
    private readonly List<IDimOverlayWindow> _activeOverlays = new();

    public IReadOnlyList<IDimOverlayWindow> ActiveOverlays => _activeOverlays;

    public DimOverlayService(
        SettingsStore settings,
        Func<IReadOnlyList<OverlayScreen>>? screenProvider = null,
        Func<IDimOverlayWindow>? windowFactory = null,
        Action<IDimOverlayWindow>? onWindowShown = null)
    {
        _settings = settings;
        _screenProvider = screenProvider ?? GetDefaultScreens;
        _windowFactory = windowFactory ?? (() => new DimOverlayWindow());
        _onWindowShown = onWindowShown ?? (w =>
        {
            if (w is Window win)
            {
                ClickThrough.Apply(win);
            }
        });
    }

    public static IReadOnlyList<OverlayScreen> GetDefaultScreens()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Screens != null)
        {
            return desktop.MainWindow.Screens.All
                .Select(s => new OverlayScreen(s.Bounds, s.Scaling))
                .ToList();
        }
        return Array.Empty<OverlayScreen>();
    }

    public void Update(string? activePeerName)
    {
        bool shouldDim = !string.IsNullOrEmpty(activePeerName) && _settings.Current.DimScreensOnControl;
        if (!shouldDim)
        {
            Hide();
            return;
        }

        Show(activePeerName!);
    }

    public void Show(string activePeerName)
    {
        double opacity = Math.Clamp(_settings.Current.DimScreensOpacity, 0.0, 0.7);
        var screens = _screenProvider();
        if (screens.Count == 0 && _activeOverlays.Count == 0)
        {
            return;
        }

        if (_activeOverlays.Count == screens.Count && _activeOverlays.Count > 0)
        {
            foreach (var overlay in _activeOverlays)
            {
                overlay.SetPeerName(activePeerName);
                overlay.SetDimOpacity(opacity);
            }
            return;
        }

        Hide();

        foreach (var screen in screens)
        {
            var window = _windowFactory();
            double scaling = screen.Scaling > 0 ? screen.Scaling : 1.0;
            window.Position = new PixelPoint(screen.Bounds.X, screen.Bounds.Y);
            window.Width = screen.Bounds.Width / scaling;
            window.Height = screen.Bounds.Height / scaling;
            window.SetPeerName(activePeerName);
            window.SetDimOpacity(opacity);
            window.Show();
            _onWindowShown?.Invoke(window);
            _activeOverlays.Add(window);
        }
    }

    public void Hide()
    {
        if (_activeOverlays.Count == 0) return;

        foreach (var overlay in _activeOverlays)
        {
            try
            {
                overlay.Close();
            }
            catch
            {
                // Ignore any teardown issues
            }
        }
        _activeOverlays.Clear();
    }

    public void Dispose()
    {
        Hide();
    }
}
