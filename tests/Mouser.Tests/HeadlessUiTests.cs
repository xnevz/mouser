using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
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
