using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Mouser.Core.Clipboard;
using Mouser.Core.Input;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Mouser.UI.Views;

namespace Mouser.UI;

public partial class App : Application
{
    private MouserHost? _host;
    private ClipboardStorageService? _clipboardStorage;
    private DispatcherTimer? _refreshTimer;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = new SettingsStore();
            _host = new MouserHost(settings, InputSinks.CreateForCurrentPlatform());
            _host.Restart();
            _clipboardStorage = new ClipboardStorageService(Path.Combine(settings.DataDirectory, "clipboard.db"));

            var vm = new MainViewModel(_host, settings, _clipboardStorage);
            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;

            window.Opened += async (_, _) =>
            {
                if (window.Clipboard is { } clipboard)
                    vm.ClipboardHistory.AttachClipboard(new AvaloniaClipboardAccess(clipboard));
                UpdateDisplays(window, vm);
                window.Screens.Changed += (_, _) => UpdateDisplays(window, vm);
                await vm.ClipboardHistory.InitializeAsync();
            };

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _refreshTimer.Tick += async (_, _) =>
            {
                vm.Refresh();
                await vm.ClipboardHistory.PollClipboardAsync();
            };
            _refreshTimer.Start();

            desktop.Exit += (_, _) =>
            {
                _refreshTimer.Stop();
                _host.Dispose();
                _clipboardStorage.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void UpdateDisplays(Window window, MainViewModel vm)
    {
        var displays = window.Screens.All
            .Select(s => new DisplayInfo(s.DisplayName ?? string.Empty, s.Bounds.X, s.Bounds.Y,
                s.Bounds.Width, s.Bounds.Height, s.Scaling, s.IsPrimary))
            .ToList();
        vm.MonitorLayout.SetDisplays(displays);
    }
}
