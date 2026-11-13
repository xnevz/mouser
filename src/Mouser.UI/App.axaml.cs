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
    private IDisposable? _hotkey;
    private QuickPasteWindow? _quickPaste;
    private MainWindow? _window;
    private bool _quitting;
    private NativeMenuItem? _pauseSharingItem;
    private NativeMenuItem? _pauseClipboardItem;
    private MainViewModel? _mainViewModel;
    private SettingsStore? _settings;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // --data-dir <folder> runs an independent instance (own settings, PIN, port and history),
            // which is how two copies on one PC can test desktop-to-desktop control.
            string? dataDir = ArgValue("--data-dir");
            SingleInstance? singleInstance = null;
            if (dataDir == null)
            {
                singleInstance = new SingleInstance();
                if (!singleInstance.IsFirst)
                {
                    singleInstance.SignalFirst();
                    desktop.Shutdown();
                    return;
                }
                singleInstance.Listen(() => Dispatcher.UIThread.Post(ShowMainWindow));
            }

            var settings = new SettingsStore(dataDir == null ? null : Path.Combine(Path.GetFullPath(dataDir), "settings.json"));
            ThemeService.Apply(settings.Current.Theme);
            _host = new MouserHost(settings, InputSinks.CreateForCurrentPlatform(), InputCaptures.CreateForCurrentPlatform());
            _host.Restart();
            _clipboardStorage = new ClipboardStorageService(Path.Combine(settings.DataDirectory, "clipboard.db"));

            var vm = new MainViewModel(_host, settings, _clipboardStorage);
            if (_host.Kvm != null) _host.Kvm.StateChanged += () => Dispatcher.UIThread.Post(vm.Refresh);
            _host.ClipReceived += (from, clip) => Dispatcher.UIThread.Post(() => _ = vm.ClipboardHistory.ApplyRemoteClipAsync(from, clip));
            _host.FileReceived += (from, path) => Dispatcher.UIThread.Post(() =>
                vm.MonitorLayout.TransferStatus = $"Received {Path.GetFileName(path)} from {from}. Saved in {Path.GetDirectoryName(path)}.");
            var window = new MainWindow { DataContext = vm };
            if (dataDir != null) window.Title = $"Mouser ({Path.GetFileName(Path.GetFullPath(dataDir))})";
            desktop.MainWindow = window;

            if (Environment.GetCommandLineArgs().Contains("--minimized") && settings.Current.CloseToTray)
                window.Opened += (_, _) => window.Hide();

            _settings = settings;
            _mainViewModel = vm;
            _window = window;

            window.Closing += (_, e) =>
            {
                if (_quitting || e.CloseReason == WindowCloseReason.OSShutdown || !_settings.Current.CloseToTray) return;
                e.Cancel = true;
                window.Hide();
            };

            window.PropertyChanged += (_, e) =>
            {
                if (e.Property == Window.WindowStateProperty && window.WindowState == WindowState.Minimized
                    && _settings.Current.MinimizeToTray && !_quitting)
                    window.Hide();
            };

            var trayIcons = TrayIcon.GetIcons(this);
            if (trayIcons != null && trayIcons.Count > 0 && trayIcons[0].Menu is { } menu)
            {
                foreach (var item in menu.Items.OfType<NativeMenuItem>())
                {
                    if (item.Header == "Pause sharing")
                    {
                        _pauseSharingItem = item;
                        item.IsChecked = _settings.Current.PauseSharing;
                        if (_settings.Current.PauseSharing)
                        {
                            _host.SetKvmEnabled(false);
                            vm.MonitorLayout.IsKvmEnabled = false;
                        }
                    }
                    else if (item.Header == "Pause clipboard")
                    {
                        _pauseClipboardItem = item;
                        item.IsChecked = _settings.Current.PauseClipboard;
                        vm.ClipboardHistory.IsPaused = _settings.Current.PauseClipboard;
                    }
                }
            }

            window.Opened += async (_, _) =>
            {
                if (window.Clipboard is { } clipboard)
                    vm.ClipboardHistory.AttachClipboard(new AvaloniaClipboardAccess(clipboard));
                UpdateDisplays(window, vm);
                vm.MonitorLayout.PickFile = async () =>
                {
                    var files = await window.StorageProvider.OpenFilePickerAsync(
                        new Avalonia.Platform.Storage.FilePickerOpenOptions { AllowMultiple = false, Title = "Send a file" });
                    return files.Count > 0 ? Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(files[0]) : null;
                };
                window.Screens.Changed += (_, _) => UpdateDisplays(window, vm);
                await vm.ClipboardHistory.InitializeAsync();
            };

            if (OperatingSystem.IsWindows())
            {
                var hotkey = new QuickPasteHotkey();
                hotkey.Pressed += previous => Dispatcher.UIThread.Post(() => ShowQuickPaste(previous, vm));
                _hotkey = hotkey;
                vm.ClipboardHistory.QuickPasteHint = hotkey.IsRegistered
                    ? $"Press {QuickPasteHotkey.Description} in any app to search this history and paste."
                    : $"{QuickPasteHotkey.Description} is taken by another app, so quick paste is unavailable.";
            }

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _refreshTimer.Tick += async (_, _) =>
            {
                vm.Refresh();
                await vm.ClipboardHistory.PollClipboardAsync();
            };
            _refreshTimer.Start();

            desktop.Exit += (_, _) =>
            {
                singleInstance?.Dispose();
                _refreshTimer.Stop();
                _hotkey?.Dispose();
                _host.Dispose();
                _clipboardStorage.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Opens the quick-paste overlay; picking a clip pastes it into <paramref name="previous"/>.</summary>
    private void ShowQuickPaste(nint previous, MainViewModel vm)
    {
        if (!OperatingSystem.IsWindows() || _clipboardStorage == null) return;
        if (_quickPaste != null)
        {
            _quickPaste.Activate();
            return;
        }

        var model = new QuickPasteViewModel(_clipboardStorage);
        var window = new QuickPasteWindow { DataContext = model };
        _quickPaste = window;
        bool closed = false;
        void Close()
        {
            if (closed) return;
            closed = true;
            window.Close();
            _quickPaste = null;
        }

        model.Cancelled += Close;
        model.PasteRequested += async text =>
        {
            if (closed) return;
            Close();
            await vm.ClipboardHistory.CopyTextAsync(text);
            var keys = InputSinks.CreateForCurrentPlatform();
            await System.Threading.Tasks.Task.Run(() => QuickPasteHotkey.PasteInto(previous, keys));
        };

        _ = model.LoadAsync();
        window.Show();
        window.Activate();
    }

    private static string? ArgValue(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static void UpdateDisplays(Window window, MainViewModel vm)
    {
        var displays = window.Screens.All
            .Select(s => new DisplayInfo(s.DisplayName ?? string.Empty, s.Bounds.X, s.Bounds.Y,
                s.Bounds.Width, s.Bounds.Height, s.Scaling, s.IsPrimary))
            .ToList();
        vm.MonitorLayout.SetDisplays(displays);
    }

    private void ShowMainWindow()
    {
        if (_window == null) return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void OnTrayClicked(object? sender, EventArgs e) => ShowMainWindow();
    private void OnTrayOpen(object? sender, EventArgs e) => ShowMainWindow();

    private void OnTrayPauseSharing(object? sender, EventArgs e)
    {
        if (_settings == null || _host == null || _mainViewModel == null) return;
        bool paused = _pauseSharingItem?.IsChecked ?? !_settings.Current.PauseSharing;
        _settings.Current.PauseSharing = paused;
        _settings.Save();
        _host.SetKvmEnabled(!paused);
        Dispatcher.UIThread.Post(() => _mainViewModel.MonitorLayout.IsKvmEnabled = !paused);
    }

    private void OnTrayPauseClipboard(object? sender, EventArgs e)
    {
        if (_settings == null || _mainViewModel == null) return;
        bool paused = _pauseClipboardItem?.IsChecked ?? !_settings.Current.PauseClipboard;
        _settings.Current.PauseClipboard = paused;
        _settings.Save();
        _mainViewModel.ClipboardHistory.IsPaused = paused;
    }

    private void OnTrayQuit(object? sender, EventArgs e)
    {
        _quitting = true;
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}
