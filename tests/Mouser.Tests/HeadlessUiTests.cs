using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Mouser.Core.Clipboard;
using Mouser.Core.Networking;
using Mouser.Core.Security;
using Mouser.Core.Services;
using Mouser.UI;
using Mouser.UI.Services;
using Mouser.UI.Views;
using Mouser.UI.ViewModels;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Mouser.Tests.TestAppBuilder))]

namespace Mouser.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Mouser.UI.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public class HeadlessUiTests
{
    private static readonly string[] NavigationTabs =
    {
        "SmartClipboard",
        "DisplayMatrix",
        "Settings",
        "Activity",
        "Snippets",
        "AndroidRemote",
        "Devices"
    };

    private static string EnsureSnapshotDirectory()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "Mouser.sln")))
        {
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == dir) break;
            dir = parent;
        }

        string snapshotDir = !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "Mouser.sln"))
            ? Path.Combine(dir, "bin", "ui-snapshots")
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-snapshots");

        Directory.CreateDirectory(snapshotDir);
        return snapshotDir;
    }

    private static void SaveSnapshot(Window window, string fileName)
    {
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame();
        if (frame == null) return;

        string snapshotDir = EnsureSnapshotDirectory();
        string snapshotPath = Path.Combine(snapshotDir, fileName);
#pragma warning disable CS0618
        using (var stream = File.Create(snapshotPath))
        {
            frame.Save(stream);
        }
#pragma warning restore CS0618

        string brainDir = @"C:\Users\abdelkarim\.gemini\antigravity\brain\9c3f1685-b5c6-48ff-977f-9f6c9c49e553\snapshots";
        if (Directory.Exists(brainDir))
        {
            File.Copy(snapshotPath, Path.Combine(brainDir, fileName), overwrite: true);
        }
    }

    [AvaloniaFact]
    public async Task CaptureAllPages_GeneratesScreenshots()
    {
        await using var fixture = new AppFixture();
        var vm = fixture.Main;
        vm.Onboarding = null; // dismiss onboarding so pages are visible

        ThemeService.Apply("Dark", "ModernMinimalist");
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // 1. Empty Clipboard History
        vm.NavigateCommand.Execute("SmartClipboard");
        Dispatcher.UIThread.RunJobs();
        SaveSnapshot(window, "page-01-clipboard-empty.png");

        // 2. Populated Clipboard History with detail card
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await fixture.Storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "clip-json-1",
            TimestampUtc = now,
            TextContent = "{\n  \"status\": \"connected\",\n  \"theme\": \"CyberpunkTerminal\",\n  \"port\": 58491\n}",
            SourceDevice = "Localhost"
        });
        await fixture.Storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "clip-url-2",
            TimestampUtc = now - 5000,
            TextContent = "https://github.com/mouser-org/mouser-cross-platform",
            SourceDevice = "MacBook Pro"
        });
        await fixture.Storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "clip-secret-3",
            TimestampUtc = now - 10000,
            TextContent = "sk-live-94857201948572018475",
            SourceDevice = "Localhost"
        });

        await vm.ClipboardHistory.ReloadAsync();
        Dispatcher.UIThread.RunJobs();
        if (vm.ClipboardHistory.FilteredClips.Count > 0)
        {
            vm.ClipboardHistory.SelectedClip = vm.ClipboardHistory.FilteredClips[0];
            Dispatcher.UIThread.RunJobs();
        }
        SaveSnapshot(window, "page-02-clipboard-populated.png");

        // 3. Devices - This Device
        vm.NavigateCommand.Execute("DisplayMatrix");
        vm.DevicesPage.SelectCommand.Execute("this");
        Dispatcher.UIThread.RunJobs();
        SaveSnapshot(window, "page-03-devices-this.png");

        // 4. Devices - Peer
        var peer = new DiscoveredPeer
        {
            DeviceId = "peer-mbp-1",
            Hostname = "MacBook-Pro-M2",
            OperatingSystem = "macOS Sequoia",
            ScreenWidth = 2560,
            ScreenHeight = 1600
        };
        vm.DevicesPage.Layout.SetPeers(new[] { peer });
        Dispatcher.UIThread.RunJobs();
        if (vm.DevicesPage.Layout.Peers.Count > 0)
        {
            vm.DevicesPage.SelectCommand.Execute(vm.DevicesPage.Layout.Peers[0]);
            Dispatcher.UIThread.RunJobs();
        }
        SaveSnapshot(window, "page-04-devices-peer.png");

        // 5. Snippets
        await fixture.Storage.SaveSnippetAsync(new Snippet
        {
            Id = "snip-1",
            Name = "Server Connection String",
            Body = "Server={{host}};Database={{db}};User Id={{user}};Password={{pwd}};"
        });
        await fixture.Storage.SaveSnippetAsync(new Snippet
        {
            Id = "snip-2",
            Name = "Email Signature",
            Body = "Best regards,\nEngineering Team\nMouser Cross-Platform"
        });
        vm.NavigateCommand.Execute("Snippets");
        await vm.Snippets.LoadSnippetsAsync();
        Dispatcher.UIThread.RunJobs();
        if (vm.Snippets.Snippets.Count > 0)
        {
            vm.Snippets.SelectedSnippet = vm.Snippets.Snippets[0];
            Dispatcher.UIThread.RunJobs();
        }
        SaveSnapshot(window, "page-05-snippets.png");

        // 6. Activity
        vm.NavigateCommand.Execute("Activity");
        vm.Activity.Entries.Add(new ActivityEvent(DateTime.UtcNow, ActivityCategory.Pairing, "Device paired: MacBook-Pro-M2 (SAS: 849204)"));
        vm.Activity.Entries.Add(new ActivityEvent(DateTime.UtcNow.AddSeconds(-20), ActivityCategory.Clipboard, "Pushed text clip (142 bytes) to MacBook-Pro-M2"));
        vm.Activity.Entries.Add(new ActivityEvent(DateTime.UtcNow.AddMinutes(-2), ActivityCategory.Discovery, "Discovered active peer on 192.168.1.145"));
        Dispatcher.UIThread.RunJobs();
        SaveSnapshot(window, "page-06-activity.png");

        // 7. Settings
        vm.NavigateCommand.Execute("Settings");
        Dispatcher.UIThread.RunJobs();
        SaveSnapshot(window, "page-07-settings.png");

        // 8. Onboarding View
        vm.Onboarding = new OnboardingViewModel(fixture.Settings, fixture.Host);
        Dispatcher.UIThread.RunJobs();
        SaveSnapshot(window, "page-08-onboarding.png");
        vm.Onboarding = null;
        Dispatcher.UIThread.RunJobs();

        window.Close();

        // 9. Lock View (standalone window)
        string lockDbPath = Path.Combine(Path.GetTempPath(), "test_lock_" + Guid.NewGuid().ToString("N") + ".db");
        var appLock = new AppLockService(lockDbPath);
        var lockWin = new Window
        {
            Width = 980,
            Height = 640,
            Content = new LockView { DataContext = new LockViewModel(appLock) }
        };
        lockWin.Show();
        Dispatcher.UIThread.RunJobs();
        SaveSnapshot(lockWin, "page-09-lock.png");
        lockWin.Close();

        // 10. Hotkeys View (standalone)
        var hotkeysWin = new Window
        {
            Width = 980,
            Height = 640,
            Content = new HotkeysView { DataContext = vm.Settings.Hotkeys }
        };
        hotkeysWin.Show();
        Dispatcher.UIThread.RunJobs();
        SaveSnapshot(hotkeysWin, "page-10-hotkeys.png");
        hotkeysWin.Close();

        // 11. Monitor Layout View (standalone)
        var monitorWin = new Window
        {
            Width = 980,
            Height = 640,
            Content = new MonitorLayoutView { DataContext = vm.DevicesPage.Layout }
        };
        monitorWin.Show();
        Dispatcher.UIThread.RunJobs();
        SaveSnapshot(monitorWin, "page-11-monitor-layout.png");
        monitorWin.Close();

        // 12. QuickPaste Window
        var qpWin = new QuickPasteWindow
        {
            Width = 560,
            Height = 420,
            DataContext = new QuickPasteViewModel(fixture.Storage, fixture.Clipboard)
        };
        qpWin.Show();
        Dispatcher.UIThread.RunJobs();
        SaveSnapshot(qpWin, "page-12-quickpaste.png");
        qpWin.Close();

        // 13. Toast Window
        var toastWin = new ToastWindow
        {
            Width = 360,
            Height = 100
        };
        toastWin.TitleBlock.Text = "Clipboard Synchronized";
        toastWin.BodyBlock.Text = "Received 142 bytes from MacBook-Pro-M2.";
        toastWin.Show();
        Dispatcher.UIThread.RunJobs();
        SaveSnapshot(toastWin, "page-13-toast.png");
        toastWin.Close();
    }

    [AvaloniaFact]
    public async Task Navigation_AllTabs_DisplayValidViewsWithoutNotFound()
    {
        await using var fixture = new AppFixture();
        var vm = fixture.Main;
        var viewLocator = new ViewLocator();

        foreach (var tab in NavigationTabs)
        {
            vm.NavigateCommand.Execute(tab);
            Dispatcher.UIThread.RunJobs();

            Assert.NotNull(vm.CurrentPage);
            var view = viewLocator.Build(vm.CurrentPage);
            Assert.NotNull(view);

            if (view is TextBlock tb && tb.Text?.StartsWith("Not Found:", StringComparison.Ordinal) == true)
            {
                Assert.Fail($"Navigation to '{tab}' produced '{tb.Text}'");
            }

            Assert.True(
                view is ClipboardHistoryView ||
                view is DevicesView ||
                view is SettingsView ||
                view is ActivityView ||
                view is SnippetsView,
                $"Unexpected view type '{view.GetType().Name}' for tab '{tab}'"
            );
        }
    }

    [AvaloniaFact]
    public async Task MainWindow_WithMainViewModel_RendersWithoutStackOverflow()
    {
        await using var fixture = new AppFixture();
        var vm = fixture.Main;
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        string[] tabs = ["DisplayMatrix", "AndroidRemote", "Settings", "Activity", "Snippets", "SmartClipboard"];
        foreach (var tab in tabs)
        {
            vm.NavigateCommand.Execute(tab);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.NotNull(window);
        window.Close();
    }

    [AvaloniaFact]
    public void DesignThemes_AllSeven_ApplyAndRenderInLightAndDark()
    {
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var designs = ThemeService.Designs;
        Assert.Equal(7, designs.Length);

        string[] modes = ["Dark", "Light"];

        foreach (var design in designs)
        {
            foreach (var mode in modes)
            {
                ThemeService.Apply(mode, design.Id);
                Dispatcher.UIThread.RunJobs();

                var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Assert.True(frame.PixelSize.Width > 0);
                Assert.True(frame.PixelSize.Height > 0);
            }
        }

        window.Close();
    }

    [AvaloniaFact]
    public void ThemeSwitching_LightAndDark_RendersNonBlankFramesAndSavesSnapshots()
    {
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        string snapshotDir = EnsureSnapshotDirectory();

        var themes = new[]
        {
            (Variant: ThemeVariant.Light, Name: "light"),
            (Variant: ThemeVariant.Dark, Name: "dark")
        };

        foreach (var (variant, name) in themes)
        {
            Application.Current!.RequestedThemeVariant = variant;
            window.RequestedThemeVariant = variant;
            Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            Assert.True(frame.PixelSize.Width > 0, "Frame width must be > 0");
            Assert.True(frame.PixelSize.Height > 0, "Frame height must be > 0");

            bool hasNonZero = false;
            bool hasDiffering = false;

            using (var fb = frame.Lock())
            {
                unsafe
                {
                    byte* ptr = (byte*)fb.Address;
                    int totalBytes = fb.RowBytes * fb.Size.Height;
                    byte firstByte = ptr[0];

                    for (int i = 0; i < totalBytes; i++)
                    {
                        if (ptr[i] != 0) hasNonZero = true;
                        if (ptr[i] != firstByte) hasDiffering = true;
                        if (hasNonZero && hasDiffering) break;
                    }
                }
            }

            Assert.True(hasNonZero, $"Rendered frame for theme '{name}' has only zero pixels.");
            Assert.True(hasDiffering, $"Rendered frame for theme '{name}' is completely blank.");

            string snapshotPath = Path.Combine(snapshotDir, $"snapshot-{name}.png");
#pragma warning disable CS0618
            using (var stream = File.Create(snapshotPath))
            {
                frame.Save(stream);
            }
#pragma warning restore CS0618

            Assert.True(File.Exists(snapshotPath), $"Snapshot '{snapshotPath}' was not created.");
            Assert.True(new FileInfo(snapshotPath).Length > 0, $"Snapshot '{snapshotPath}' is empty.");

            // Also copy to test binary folder for artifact collection
            string localDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-snapshots");
            if (!string.Equals(Path.GetFullPath(localDir), Path.GetFullPath(snapshotDir), StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(localDir);
                File.Copy(snapshotPath, Path.Combine(localDir, $"snapshot-{name}.png"), overwrite: true);
            }
        }

        window.Close();
    }
}
