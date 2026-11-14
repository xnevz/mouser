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
using Mouser.Core.Security;
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
    private IDisposable? _panicHotkey;
    private QuickPasteWindow? _quickPaste;
    private MainWindow? _window;
    private bool _quitting;
    private bool _quitAfterUnlock;
    private bool _hasShownTrayToast;
    private NativeMenuItem? _lockItem;
    private NativeMenuItem? _pauseSharingItem;
    private NativeMenuItem? _pauseClipboardItem;
    private MainViewModel? _mainViewModel;
    private SettingsStore? _settings;
    private AppLockService? _appLock;
    private ClipboardWatcher? _clipboardWatcher;
    private IClipboardAccess? _clipboardAccess;
    private DimOverlayService? _dimOverlayService;

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
            ToastService.Initialize(settings);
            var appLock = new AppLockService(Path.Combine(settings.DataDirectory, "lock.json"));
            _appLock = appLock;
            _appLock.Locked += () =>
            {
                try
                {
                    if (Dispatcher.UIThread.CheckAccess())
                        HandleLocked();
                    else
                        Dispatcher.UIThread.Post(HandleLocked);
                }
                catch
                {
                    HandleLocked();
                }
            };
            _appLock.Unlocked += () =>
            {
                try
                {
                    if (Dispatcher.UIThread.CheckAccess())
                        HandleUnlocked();
                    else
                        Dispatcher.UIThread.Post(HandleUnlocked);
                }
                catch
                {
                    HandleUnlocked();
                }
            };
            _host = new MouserHost(settings, InputSinks.CreateForCurrentPlatform(), InputCaptures.CreateForCurrentPlatform());
            if (appLock.IsLocked)
            {
                _host.SetLocked(true);
            }
            _host.Restart();
            _clipboardStorage = new ClipboardStorageService(Path.Combine(settings.DataDirectory, "clipboard.db"));

            var vm = new MainViewModel(_host, settings, _clipboardStorage, appLock: appLock);
            _clipboardWatcher = new ClipboardWatcher();
            _clipboardWatcher.Changed += () => Dispatcher.UIThread.Post(async () =>
            {
                if (_appLock is { IsLocked: true }) return;
                await vm.ClipboardHistory.PollClipboardAsync();
            });
            var window = new MainWindow { DataContext = vm };
            if (window.Clipboard is { } cb)
            {
                _clipboardAccess = new AvaloniaClipboardAccess(cb);
                vm.AttachClipboard(_clipboardAccess);
            }
            if (dataDir != null) window.Title = $"Mouser ({Path.GetFileName(Path.GetFullPath(dataDir))})";
            desktop.MainWindow = window;

            _settings = settings;
            _mainViewModel = vm;
            _window = window;

            _dimOverlayService = new DimOverlayService(settings, () =>
            {
                if (window.Screens != null)
                {
                    return window.Screens.All.Select(s => new OverlayScreen(s.Bounds, s.Scaling)).ToList();
                }
                return Array.Empty<OverlayScreen>();
            });
            vm.Settings.DimSettingsChanged += () => Dispatcher.UIThread.Post(() => _dimOverlayService?.Update(_host?.Kvm?.ActivePeerName));

            if (_host.Kvm != null)
            {
                _host.Kvm.StateChanged += () => Dispatcher.UIThread.Post(() =>
                {
                    vm.Refresh();
                    string? peer = _host.Kvm.ActivePeerName;
                    _dimOverlayService?.Update(peer);

                    if (_window != null && _window.IsVisible && _window.IsActive) return;
                    if (!string.IsNullOrEmpty(peer))
                        ToastService.Show($"Controlling {peer}", "Cursor is on the other computer");
                    else
                        ToastService.Show("Back on this computer", "Cursor returned to this screen");
                });
            }

            _host.ClipReceived += (from, clip) => Dispatcher.UIThread.Post(() =>
            {
                _ = vm.ClipboardHistory.ApplyRemoteClipAsync(from, clip);
                if (_window == null || !_window.IsVisible || !_window.IsActive)
                {
                    string snippet = clip.TextContent.Length > 60 ? clip.TextContent[..60] + "…" : clip.TextContent;
                    ToastService.Show($"Clip from {from}", snippet);
                }
            });

            _host.FileReceived += (from, path) => Dispatcher.UIThread.Post(() =>
            {
                vm.MonitorLayout.TransferStatus = $"Received {Path.GetFileName(path)} from {from}. Saved in {Path.GetDirectoryName(path)}.";
                if (_window == null || !_window.IsVisible || !_window.IsActive)
                    ToastService.Show($"File from {from}", Path.GetFileName(path));
            });

            if (Environment.GetCommandLineArgs().Contains("--minimized") && settings.Current.CloseToTray)
                window.Opened += (_, _) => HideToTray(window);

            window.Closing += (_, e) =>
            {
                if (_quitting || e.CloseReason == WindowCloseReason.OSShutdown) return;
                if (_appLock is { IsLocked: true })
                {
                    e.Cancel = true;
                    HideToTray(window);
                    return;
                }
                if (!_settings.Current.CloseToTray) return;
                e.Cancel = true;
                HideToTray(window);
            };

            window.PropertyChanged += (_, e) =>
            {
                if (e.Property == Window.WindowStateProperty && window.WindowState == WindowState.Minimized
                    && _settings.Current.MinimizeToTray && !_quitting)
                    HideToTray(window);
            };

            var trayIcons = TrayIcon.GetIcons(this);
            if (trayIcons != null && trayIcons.Count > 0 && trayIcons[0].Menu is { } menu)
            {
                foreach (var item in menu.Items.OfType<NativeMenuItem>())
                {
                    if (item.Header == "Lock")
                    {
                        _lockItem = item;
                        item.IsVisible = _appLock?.IsConfigured ?? false;
                    }
                    else if (item.Header == "Pause sharing")
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
                if (_clipboardAccess == null && window.Clipboard is { } clipboard)
                {
                    _clipboardAccess = new AvaloniaClipboardAccess(clipboard);
                    vm.AttachClipboard(_clipboardAccess);
                }
                UpdateDisplays(window, vm);
                vm.MonitorLayout.PickFile = async () =>
                {
                    var files = await window.StorageProvider.OpenFilePickerAsync(
                        new Avalonia.Platform.Storage.FilePickerOpenOptions { AllowMultiple = false, Title = "Send a file" });
                    return files.Count > 0 ? Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(files[0]) : null;
                };
                vm.ClipboardHistory.PickSavePath = async () =>
                {
                    var file = await window.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                    {
                        Title = "Save image as",
                        DefaultExtension = "png",
                        SuggestedFileName = "image.png"
                    });
                    return file != null ? Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(file) : null;
                };
                window.Screens.Changed += (_, _) =>
                {
                    UpdateDisplays(window, vm);
                    _dimOverlayService?.Update(_host?.Kvm?.ActivePeerName);
                };
                await vm.ClipboardHistory.InitializeAsync();
            };

            if (OperatingSystem.IsWindows())
            {
                var hotkeyString = settings.Current.QuickPasteHotkey;
                var hotkey = new QuickPasteHotkey(hotkeyString);
                hotkey.Pressed += previous => Dispatcher.UIThread.Post(() => ShowQuickPaste(previous, vm));
                _hotkey = hotkey;
                vm.ClipboardHistory.QuickPasteHint = hotkey.IsRegistered
                    ? $"Press {hotkey.HotkeyText} in any app to search this history and paste."
                    : $"{hotkey.HotkeyText} is taken by another app, so quick paste is unavailable.";

                vm.Settings.Hotkeys.QuickPasteHotkeyChanged += () =>
                {
                    if (!OperatingSystem.IsWindows()) return;
                    _hotkey?.Dispose();
                    var newString = settings.Current.QuickPasteHotkey;
                    var newHotkey = new QuickPasteHotkey(newString);
                    newHotkey.Pressed += previous => Dispatcher.UIThread.Post(() => ShowQuickPaste(previous, vm));
                    _hotkey = newHotkey;
                    vm.ClipboardHistory.QuickPasteHint = newHotkey.IsRegistered
                        ? $"Press {newHotkey.HotkeyText} in any app to search this history and paste."
                        : $"{newHotkey.HotkeyText} is taken by another app, so quick paste is unavailable.";
                };

                if (!string.IsNullOrWhiteSpace(settings.Current.PanicWipeHotkey))
                {
                    var panic = new PanicWipeHotkey(settings.Current.PanicWipeHotkey);
                    if (panic.IsRegistered)
                    {
                        panic.Pressed += () => Dispatcher.UIThread.Post(async () =>
                        {
                            if (_clipboardStorage != null)
                            {
                                int removed = await _clipboardStorage.ClearAsync(includeFavorites: true);
                                await vm.ClipboardHistory.ReloadAsync();
                                await vm.Settings.RefreshHistorySizeAsync();
                                ToastService.Show("History wiped", $"Deleted {removed} clips.");
                            }
                        });
                        _panicHotkey = panic;
                    }
                    else
                    {
                        panic.Dispose();
                    }
                }
            }

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _refreshTimer.Tick += async (_, _) =>
            {
                if (ShouldAutoLock(_appLock, settings.Current, WindowsSession.IsWorkstationLocked(), WindowsSession.IdleTime()))
                {
                    _appLock?.Lock();
                }

                UpdateLockTrayItem();
                vm.Refresh();
                await vm.ClipboardHistory.PollClipboardAsync();
            };
            _refreshTimer.Start();

            desktop.Exit += (_, _) =>
            {
                if (settings.Current.ClearOnExit && _clipboardStorage != null)
                {
                    try
                    {
                        _clipboardStorage.ClearAsync(includeFavorites: false).GetAwaiter().GetResult();
                    }
                    catch { }
                }
                singleInstance?.Dispose();
                _clipboardWatcher?.Dispose();
                _refreshTimer.Stop();
                _hotkey?.Dispose();
                _panicHotkey?.Dispose();
                _host.Dispose();
                _clipboardStorage?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Opens the quick-paste overlay; picking a clip pastes it into <paramref name="previous"/>.</summary>
    internal bool ShowQuickPaste(nint previous, MainViewModel vm)
    {
        if (_appLock is { IsLocked: true }) return false;
        if (!OperatingSystem.IsWindows() || _clipboardStorage == null) return false;
        if (_quickPaste != null)
        {
            _quickPaste.Activate();
            return true;
        }

        var model = new QuickPasteViewModel(_clipboardStorage, _clipboardAccess);
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
        model.PasteRequested += async (text, rich) =>
        {
            if (closed) return;
            Close();
            var targetItem = model.LastPastedItem ?? model.Selected;
            if (targetItem?.IsImage == true)
            {
                await vm.ClipboardHistory.CopyClipCommand.ExecuteAsync(targetItem);
            }
            else if (_clipboardAccess != null)
            {
                if (rich && targetItem?.HtmlContent is { Length: > 0 } html)
                {
                    await _clipboardAccess.SetSnapshotAsync(new(text, html, null, null));
                }
                else
                {
                    await _clipboardAccess.SetSnapshotAsync(new(text, null, null, null));
                }
            }
            else
            {
                await vm.ClipboardHistory.CopyTextAsync(text);
            }
            var keys = InputSinks.CreateForCurrentPlatform();
            if (OperatingSystem.IsWindows())
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        QuickPasteHotkey.PasteInto(previous, keys);
                    }
                });
            }
        };

        _ = model.LoadAsync();
        window.Show();
        window.Activate();
        return true;
    }

    private static string? ArgValue(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private void UpdateDisplays(Window window, MainViewModel vm)
    {
        var screens = window.Screens.All;
        var displays = screens
            .Select(s => new DisplayInfo(s.DisplayName ?? string.Empty, s.Bounds.X, s.Bounds.Y,
                s.Bounds.Width, s.Bounds.Height, s.Scaling, s.IsPrimary))
            .ToList();
        var monitors = screens.Select(s => new Mouser.Core.Geometry.Monitor(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)).ToList();
        _host?.SetLocalMonitors(monitors);
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

    private void HideToTray(Window window)
    {
        window.Hide();
        if (!_hasShownTrayToast)
        {
            _hasShownTrayToast = true;
            ToastService.Show("Mouser is still running", "Mouser is still running in the tray.");
        }
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

    internal static bool ShouldAutoLock(AppLockService? appLock, AppSettings settings, bool isWorkstationLocked, TimeSpan idleTime)
    {
        return appLock is { IsConfigured: true, IsLocked: false } &&
            ((settings.LockWithWindows && isWorkstationLocked) ||
             (settings.AutoLockMinutes > 0 && idleTime >= TimeSpan.FromMinutes(settings.AutoLockMinutes)));
    }

    internal bool QuitAfterUnlock => _quitAfterUnlock;
    internal bool Quitting => _quitting;

    internal void SetAppLockForTesting(AppLockService appLock)
    {
        _appLock = appLock;
        _appLock.Locked += HandleLocked;
        _appLock.Unlocked += HandleUnlocked;
    }

    private void HandleLocked()
    {
        _dimOverlayService?.Hide();
        if (_quickPaste != null)
        {
            _quickPaste.Close();
            _quickPaste = null;
        }
        UpdateLockTrayItem();
    }

    private void HandleUnlocked()
    {
        UpdateLockTrayItem();
        if (_quitAfterUnlock)
        {
            _quitting = true;
            (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }
    }

    private void UpdateLockTrayItem()
    {
        if (_lockItem == null) return;
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
                _lockItem.IsVisible = _appLock?.IsConfigured ?? false;
            else
                Dispatcher.UIThread.Post(() => _lockItem.IsVisible = _appLock?.IsConfigured ?? false);
        }
        catch
        {
            _lockItem.IsVisible = _appLock?.IsConfigured ?? false;
        }
    }

    private void OnTrayLock(object? sender, EventArgs e)
    {
        if (_appLock is { IsConfigured: true })
        {
            _appLock.Lock();
        }
    }

    private void OnTrayQuit(object? sender, EventArgs e)
    {
        RequestQuit();
    }

    internal bool RequestQuit()
    {
        if (_appLock is { IsLocked: true })
        {
            ShowMainWindow();
            _quitAfterUnlock = true;
            return false;
        }
        _quitting = true;
        _dimOverlayService?.Hide();
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        return true;
    }
}
