using System.Net;
using Mouser.Core.Clipboard;
using Mouser.Core.Geometry;
using Mouser.Core.Input;
using Mouser.Core.Kvm;
using Mouser.Core.Networking;
using Mouser.Core.Security;
using Mouser.Core.Services;
using Mouser.UI;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Mouser.UI.Views;
using Monitor = Mouser.Core.Geometry.Monitor;

namespace Mouser.Tests;

public sealed class FakeClipboard : IClipboardAccess
{
    public string? Text { get; set; }
    public string? Html { get; set; }
    public byte[]? ImagePng { get; set; }
    public IReadOnlyList<string>? Files { get; set; }

    public Task<string?> GetTextAsync() => Task.FromResult(Text);
    public Task SetTextAsync(string text) { Text = text; return Task.CompletedTask; }

    public Task<ClipboardSnapshot> GetSnapshotAsync() =>
        Task.FromResult(new ClipboardSnapshot(Text, Html, ImagePng, Files));

    public Task SetSnapshotAsync(ClipboardSnapshot s)
    {
        Text = s.Text;
        Html = s.Html;
        ImagePng = s.ImagePng;
        Files = s.Files;
        return Task.CompletedTask;
    }
}

/// <summary>A full app instance on temp storage with networking on random ports.</summary>
public sealed class AppFixture : IAsyncDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mouser_vm_" + Guid.NewGuid().ToString("N"));

    public SettingsStore Settings { get; }
    public MouserHost Host { get; }
    public ClipboardStorageService Storage { get; }
    public RecordingSink Sink { get; } = new();
    public FakeClipboard Clipboard { get; } = new();
    public FakeCapture? Capture { get; }
    public MainViewModel Main { get; }
    public AppLockService? AppLock { get; }

    public string Downloads => Path.Combine(_dir, "downloads");

    public AppFixture(int? discoveryPort = null, AppLockService? appLock = null, FakeCapture? capture = null)
    {
        Capture = capture;
        Settings = new SettingsStore(Path.Combine(_dir, "settings.json"));
        Settings.Current.InputPort = 0; // any free port
        Host = new MouserHost(Settings, Sink, capture: capture, discoveryPort: discoveryPort ?? Random.Shared.Next(40_000, 50_000),
            downloadDirectory: Path.Combine(_dir, "downloads"));
        AppLock = appLock;
        if (appLock?.IsLocked == true)
        {
            Host.SetLocked(true);
        }
        Host.Restart();
        Storage = new ClipboardStorageService(Path.Combine(_dir, "clipboard.db"));
        Main = new MainViewModel(Host, Settings, Storage, appLock: appLock);
        Main.DispatchToUI = a => a();
        Main.ClipboardHistory.AttachClipboard(Clipboard);
        Main.Activity.AttachClipboard(Clipboard);
    }

    public async ValueTask DisposeAsync()
    {
        Host.Dispose();
        await Storage.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}

public class ViewModelTests
{
    [Fact]
    public async Task Navigation_SwitchesPagesAndActiveTab()
    {
        await using var app = new AppFixture();
        var vm = app.Main;

        Assert.True(vm.IsClipboardTab);
        Assert.IsType<ClipboardHistoryViewModel>(vm.CurrentPage);

        vm.NavigateCommand.Execute("AndroidRemote");
        Assert.True(vm.IsDisplaysTab);
        Assert.IsType<DevicesViewModel>(vm.CurrentPage);

        vm.NavigateCommand.Execute("DisplayMatrix");
        Assert.True(vm.IsDisplaysTab);
        Assert.IsType<DevicesViewModel>(vm.CurrentPage);

        vm.NavigateCommand.Execute("Settings");
        Assert.True(vm.IsSettingsTab);
        Assert.IsType<SettingsViewModel>(vm.CurrentPage);

        vm.NavigateCommand.Execute("Activity");
        Assert.True(vm.IsActivityTab);
        Assert.IsType<ActivityViewModel>(vm.CurrentPage);
    }

    [Fact]
    public async Task NoFakeDataOnFirstRun()
    {
        await using var app = new AppFixture();
        await app.Main.ClipboardHistory.InitializeAsync();

        Assert.Empty(app.Main.AndroidRemote.Clients);
        Assert.Empty(app.Main.ClipboardHistory.FilteredClips);
        Assert.Empty(app.Main.MonitorLayout.Screens);
        Assert.Equal("Ready for phones", app.Main.SystemStatusTitle);
    }

    [Fact]
    public async Task PhoneRemote_ListsConnectedPhone()
    {
        await using var app = new AppFixture();
        var remote = app.Main.AndroidRemote;
        Assert.True(remote.IsAccepting);

        using var phone = new FakePhone(app.Host.Input!.BoundPort);
        phone.Send(InputMotionPacket.TypeMotion, 3, 4);
        await Wait.UntilAsync(() => app.Sink.Snapshot().Length == 1);

        app.Main.Refresh();
        var client = Assert.Single(remote.Clients);
        Assert.Equal("127.0.0.1", client.Address);
        Assert.True(client.IsActive);
        Assert.Equal(1, remote.ActiveClientCount);
        Assert.Equal("1 device connected", app.Main.SystemStatusTitle);

        remote.ForgetCommand.Execute(client);
        Assert.Empty(remote.Clients);
    }

    [Fact]
    public async Task Sidebar_ListsPairedPhoneAsOnline()
    {
        await using var app = new AppFixture();
        using var phone = new FakePhone(app.Host.Input!.BoundPort);
        phone.Send(InputMotionPacket.TypeMotion, 3, 4);
        await Wait.UntilAsync(() => app.Sink.Snapshot().Length == 1);

        app.Main.Refresh();
        var device = Assert.Single(app.Main.Devices);
        Assert.Equal(DeviceKind.Phone, device.Kind);
        Assert.True(device.IsOnline);
        Assert.Equal(1, app.Main.OnlineDeviceCount);
    }

    [Fact]
    public void ShowLibrary_NavigatesToClipboardAndSetsLibrary()
    {
        var app = new AppFixture();
        app.Main.ShowLibraryCommand.Execute("Starred");
        Assert.Same(app.Main.ClipboardHistory, app.Main.CurrentPage);
        Assert.Equal("Starred", app.Main.ClipboardHistory.SelectedLibrary);
    }

    [Fact]
    public void FocusSearch_NavigatesAndFiresEvent()
    {
        var app = new AppFixture();
        bool fired = false;
        app.Main.FocusSearchRequested += () => fired = true;
        app.Main.FocusSearchCommand.Execute(null);
        Assert.True(fired);
        Assert.Same(app.Main.ClipboardHistory, app.Main.CurrentPage);
    }

    [Fact]
    public async Task PhoneRemote_ToggleStopsAndStartsListening()
    {
        await using var app = new AppFixture();
        var remote = app.Main.AndroidRemote;

        remote.ToggleAcceptingCommand.Execute(null);
        Assert.False(remote.IsAccepting);
        Assert.Null(app.Host.Input);
        app.Main.Refresh();
        Assert.Equal("Remote input off", app.Main.SystemStatusTitle);

        remote.ToggleAcceptingCommand.Execute(null);
        Assert.True(remote.IsAccepting);
    }

    [Fact]
    public async Task Host_ReportsPortInUse()
    {
        await using var first = new AppFixture();
        await using var second = new AppFixture();
        second.Settings.Current.InputPort = first.Host.Input!.BoundPort;

        second.Host.Restart();

        Assert.Null(second.Host.Input);
        Assert.Contains("already in use", second.Host.InputError);
        second.Main.Refresh();
        Assert.Equal("Not listening", second.Main.SystemStatusTitle);
    }

    [Fact]
    public async Task Clipboard_CapturesCopiedText_AndSkipsSecrets()
    {
        await using var app = new AppFixture();
        var clips = app.Main.ClipboardHistory;
        await clips.InitializeAsync();

        app.Clipboard.Text = "https://avaloniaui.net/docs";
        Assert.True(await clips.PollClipboardAsync());
        Assert.False(await clips.PollClipboardAsync()); // unchanged clipboard is not re-added

        app.Clipboard.Text = "ghp_" + new string('a', 36);
        Assert.False(await clips.PollClipboardAsync());
        Assert.Contains("secret", clips.StatusMessage);

        var clip = Assert.Single(clips.FilteredClips);
        Assert.Equal(ClipboardContentType.Link, clip.Kind);
        Assert.Equal(Environment.MachineName, clip.SourceDevice);
    }

    [Fact]
    public async Task Clipboard_SearchFilterStarDeleteAndClear()
    {
        await using var app = new AppFixture();
        var clips = app.Main.ClipboardHistory;
        await clips.InitializeAsync();
        await clips.CaptureAsync("Weekly grocery list: apples, milk");
        await clips.CaptureAsync("var total = items.Sum(i => i.Price);");
        await clips.CaptureAsync("https://example.com/report");
        Assert.Equal(3, clips.AllCount);

        clips.SearchQuery = "apples";
        await clips.ReloadAsync();
        Assert.Contains("grocery", Assert.Single(clips.FilteredClips).Content);

        clips.SearchQuery = "";
        clips.SetFilterCommand.Execute("Code");
        await clips.ReloadAsync();
        Assert.Equal(ClipboardContentType.Code, Assert.Single(clips.FilteredClips).Kind);

        clips.SetFilterCommand.Execute("All");
        await clips.ReloadAsync();
        var link = clips.FilteredClips.First(c => c.Kind == ClipboardContentType.Link);
        await clips.ToggleStarCommand.ExecuteAsync(link);
        Assert.Equal(1, clips.StarredCount);

        clips.SetLibraryCommand.Execute("Starred");
        await clips.ReloadAsync();
        Assert.Equal(link.Id, Assert.Single(clips.FilteredClips).Id);

        clips.SetLibraryCommand.Execute("All");
        await clips.ReloadAsync();
        await clips.DeleteClipCommand.ExecuteAsync(clips.FilteredClips.First(c => c.Kind == ClipboardContentType.Code));
        Assert.Equal(2, clips.AllCount);

        await clips.ClearAllCommand.ExecuteAsync(null);
        Assert.Equal(1, clips.AllCount); // starred clip survives
    }

    [Fact]
    public async Task Clipboard_CopyBackDoesNotCreateADuplicate()
    {
        await using var app = new AppFixture();
        var clips = app.Main.ClipboardHistory;
        await clips.InitializeAsync();
        await clips.CaptureAsync("first");
        await clips.CaptureAsync("second");

        await clips.CopyClipCommand.ExecuteAsync(clips.FilteredClips.Single(c => c.Content == "first"));

        Assert.Equal("first", app.Clipboard.Text);
        Assert.False(await clips.PollClipboardAsync());
        Assert.Equal(2, clips.AllCount);
    }

    [Fact]
    public async Task Clipboard_RespectsDisabledSetting()
    {
        await using var app = new AppFixture();
        await app.Main.ClipboardHistory.InitializeAsync();
        app.Main.Settings.ClipboardHistoryEnabled = false;

        Assert.False(await app.Main.ClipboardHistory.CaptureAsync("should not be saved"));
        Assert.False(app.Main.ClipboardHistory.IsCaptureEnabled);
        Assert.False(new SettingsStore(app.Settings.FilePath).Current.ClipboardHistoryEnabled);
    }

    [Fact]
    public async Task ClipboardHistory_ComputesDayHeadersAndCounts()
    {
        await using var app = new AppFixture();
        var history = app.Main.ClipboardHistory;
        await history.InitializeAsync();

        await history.CaptureAsync("clip 1 today");
        await history.CaptureAsync("clip 2 today");

        var twoDaysAgo = DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeMilliseconds();
        await app.Storage.SaveClipAsync(new ClipboardPayload
        {
            TextContent = "clip two days ago",
            TimestampUtc = twoDaysAgo,
            SourceDevice = Environment.MachineName
        });

        await history.ReloadAsync();

        Assert.Equal(3, history.FilteredClips.Count);
        Assert.True(history.FilteredClips[0].ShowDayHeader);
        Assert.Equal(2, history.FilteredClips[0].DayCount);
        Assert.Equal("Today", history.FilteredClips[0].DayLabel);

        Assert.False(history.FilteredClips[1].ShowDayHeader);
        Assert.Equal(0, history.FilteredClips[1].DayCount);

        Assert.True(history.FilteredClips[2].ShowDayHeader);
        Assert.Equal(1, history.FilteredClips[2].DayCount);
    }

    [Fact]
    public void Displays_AreScaledToFitAndKeepTheirArrangement()
    {
        var vm = new MonitorLayoutViewModel();
        vm.SetDisplays(new[]
        {
            new DisplayInfo("Laptop", 0, 0, 1920, 1080, 1.25, true),
            new DisplayInfo("External", 1920, -200, 2560, 1440, 1.0, false)
        });

        Assert.Equal(2, vm.Screens.Count);
        var primary = vm.Screens[0];
        var external = vm.Screens[1];
        Assert.True(primary.IsPrimary);
        Assert.Equal("125%", primary.ScalingText);
        Assert.Equal("2560 × 1440", external.Resolution);

        Assert.True(external.PositionX > primary.PositionX); // to the right
        Assert.True(external.PositionY < primary.PositionY); // and higher, like the real layout
        Assert.True(external.DisplayWidth > primary.DisplayWidth);
        foreach (var s in vm.Screens)
        {
            Assert.InRange(s.PositionX, 0, MonitorLayoutViewModel.CanvasWidth - s.DisplayWidth);
            Assert.InRange(s.PositionY, 0, MonitorLayoutViewModel.CanvasHeight - s.DisplayHeight);
        }
        Assert.Contains("2 displays", vm.DesktopSummary);
        Assert.Same(primary, vm.SelectedScreen);

        vm.SelectScreenCommand.Execute(external);
        Assert.True(external.IsSelected);
        Assert.False(primary.IsSelected);
    }

    [Fact]
    public void Peers_AreListedFromDiscovery()
    {
        var vm = new MonitorLayoutViewModel();
        vm.SetPeers(new[]
        {
            new DiscoveredPeer { DeviceId = "1", Hostname = "studio-mac", OperatingSystem = "macOS", IpAddress = IPAddress.Parse("192.168.1.9"), MotionPort = 58491 }
        });

        var peer = Assert.Single(vm.Peers);
        Assert.Equal("192.168.1.9:58491", peer.Address);
        Assert.True(vm.HasPeers);
    }

    [Fact]
    public void MonitorLayout_PeerWithTwoMonitors_DrawsTwoPeerNodes()
    {
        var vm = new MonitorLayoutViewModel();
        vm.SetDisplays(new[]
        {
            new DisplayInfo("Laptop", 0, 0, 1920, 1080, 1.0, true)
        });

        var peer = new DiscoveredPeer
        {
            DeviceId = "peer1",
            Hostname = "studio-pc",
            OperatingSystem = "Windows",
            ScreenWidth = 3840,
            ScreenHeight = 1080,
            Monitors = new[]
            {
                new Monitor(0, 0, 1920, 1080),
                new Monitor(1920, 0, 1920, 1080)
            }
        };

        vm.SetPeers(new[] { peer });
        var peerVm = Assert.Single(vm.Peers);
        peerVm.Status = PeerLinkStatus.Paired;
        peerVm.Placement = PeerPlacement.Right;

        Assert.Equal(2, vm.Screens.Count(s => s.IsPeer));
        var peerNodes = vm.Screens.Where(s => s.IsPeer).ToList();
        Assert.All(peerNodes, s => Assert.Equal("1920 × 1080", s.Resolution));
        Assert.Equal("studio-pc (1)", peerNodes[0].Name);
        Assert.Equal("studio-pc (2)", peerNodes[1].Name);
        Assert.True(peerNodes[1].PositionX > peerNodes[0].PositionX);
    }

    [Fact]
    public void MonitorLayout_SetPeerPlacementAndOffset_PersistsInSettingsAndShiftsNodePosition()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_layout_offset_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tmp);
            var vm = new MonitorLayoutViewModel(settings: store);
            vm.SetDisplays(new[]
            {
                new DisplayInfo("Laptop", 0, 0, 1920, 1080, 1.0, true)
            });

            var peer = new DiscoveredPeer
            {
                DeviceId = "peer1",
                Hostname = "studio-pc",
                OperatingSystem = "Windows",
                ScreenWidth = 1920,
                ScreenHeight = 1080
            };

            vm.SetPeers(new[] { peer });
            var peerVm = Assert.Single(vm.Peers);
            peerVm.Status = PeerLinkStatus.Paired;

            // 1. Initial placement at Right with 0 offset
            vm.SetPeerPlacementAndOffset("peer1", PeerPlacement.Right, 0, 0);
            var node = Assert.Single(vm.Screens.Where(s => s.IsPeer));
            double initialY = node.PositionY;

            // 2. Set offset Y to 300
            vm.SetPeerPlacementAndOffset("peer1", PeerPlacement.Right, 0, 300);
            Assert.Equal(300, store.Current.Peers["peer1"].OffsetY);
            Assert.Equal(0, store.Current.Peers["peer1"].OffsetX);
            Assert.Equal(PeerPlacement.Right, store.Current.Peers["peer1"].Placement);

            // Re-fetch node after relayout
            var shiftedNode = Assert.Single(vm.Screens.Where(s => s.IsPeer));
            Assert.True(shiftedNode.PositionY > initialY);

            // Verify persisted to file
            var reloaded = new SettingsStore(tmp);
            Assert.True(reloaded.Current.Peers.TryGetValue("peer1", out var reloadedPeer));
            Assert.Equal(300, reloadedPeer.OffsetY);
            Assert.Equal(PeerPlacement.Right, reloadedPeer.Placement);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void MonitorLayout_SnappingLogic_SnapsToClosestEdgeAndAlignsTopOrCenter()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_layout_snap_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tmp);
            var vm = new MonitorLayoutViewModel(settings: store);
            vm.SetDisplays(new[]
            {
                new DisplayInfo("Laptop", 0, 0, 1920, 1080, 1.0, true)
            });

            var peer = new DiscoveredPeer
            {
                DeviceId = "peer1",
                Hostname = "studio-pc",
                OperatingSystem = "Windows",
                ScreenWidth = 1920,
                ScreenHeight = 1080
            };

            vm.SetPeers(new[] { peer });
            var peerVm = Assert.Single(vm.Peers);
            peerVm.Status = PeerLinkStatus.Paired;
            vm.SetPeerPlacementAndOffset("peer1", PeerPlacement.Right, 0, 0);

            var peerNode = Assert.Single(vm.Screens.Where(s => s.IsPeer));
            var local = vm.LocalDesktop!;

            // Case A: Drag near right edge (within 24px) and top edge difference <= 12px -> snaps offset to 0
            peerNode.PositionX = peerNode.PositionX + 5; // close to right edge
            peerNode.PositionY = vm.CanvasOriginY + 6;   // 6px difference from top -> <= 12px
            MonitorLayoutView.ApplySnappingAndCommit(vm, peerNode, peerVm, local);

            Assert.Equal(PeerPlacement.Right, store.Current.Peers["peer1"].Placement);
            Assert.Equal(0, store.Current.Peers["peer1"].OffsetY);

            // Case B: Drag below screen with left alignment <= 12px -> snaps placement to Below and offsetX to 0
            double localCanvasX = vm.CanvasOriginX + (local.X - vm.CanvasMinX) * vm.CanvasScale;
            double localCanvasBottom = vm.CanvasOriginY + (local.Bottom - vm.CanvasMinY) * vm.CanvasScale;
            peerNode.PositionX = localCanvasX + 8; // <= 12px from left
            peerNode.PositionY = localCanvasBottom + 10; // near bottom edge
            MonitorLayoutView.ApplySnappingAndCommit(vm, peerNode, peerVm, local);

            Assert.Equal(PeerPlacement.Below, store.Current.Peers["peer1"].Placement);
            Assert.Equal(0, store.Current.Peers["peer1"].OffsetX);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public async Task Settings_PersistAndValidatePort()
    {
        await using var app = new AppFixture();
        var settings = app.Main.Settings;

        settings.ClipboardRetentionDays = 7;
        settings.SkipSensitiveClips = false;
        settings.InputPort = 80;
        settings.ApplyPortCommand.Execute(null);
        Assert.Contains("between 1024 and 65535", settings.StatusMessage);

        var reloaded = new SettingsStore(app.Settings.FilePath).Current;
        Assert.Equal(7, reloaded.ClipboardRetentionDays);
        Assert.False(reloaded.SkipSensitiveClips);
        Assert.NotEqual(80, reloaded.InputPort); // invalid port was not saved
    }

    [Fact]
    public void SettingsStore_RecoversFromCorruptFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mouser_settings_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");
        try
        {
            var store = new SettingsStore(path);
            Assert.True(File.Exists(path + ".corrupt"));
            Assert.NotEmpty(store.Current.DeviceId);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".corrupt");
        }
    }

    [Fact]
    public async Task Theme_DefaultsToSystem()
    {
        var app = new AppFixture();
        await using var _ = app;
        Assert.Equal("System", app.Settings.Current.Theme);
    }

    [Fact]
    public async Task Theme_SettingPersists()
    {
        var app = new AppFixture();
        await using var _ = app;
        app.Main.Settings.Theme = "Light";
        var reloaded = new SettingsStore(app.Settings.FilePath).Current;
        Assert.Equal("Light", reloaded.Theme);
    }

    [Fact]
    public void Theme_InvalidValueFallsBackToSystem()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mouser_theme_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{\n  \"Theme\": \"Banana\"\n}");
        try
        {
            var store = new SettingsStore(path);
            Assert.Equal("System", store.Current.Theme);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Settings_FromOlderVersionWithRemovedDesignKeys_StillLoad()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mouser_legacy_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{\n  \"Theme\": \"Dark\",\n  \"DesignTheme\": \"CyberpunkTerminal\",\n  \"UiCornerRadius\": \"Sharp (0px)\",\n  \"CustomAccentColor\": \"#ff00ff\"\n}");
        try
        {
            Assert.Equal("Dark", new SettingsStore(path).Current.Theme);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PauseClipboard_DoesNotCapture()
    {
        var app = new AppFixture();
        await using var _ = app;
        var history = app.Main.ClipboardHistory;
        await history.InitializeAsync();
        app.Clipboard.Text = "hello";
        history.IsPaused = true;
        Assert.False(await history.PollClipboardAsync());
        Assert.Empty(history.FilteredClips);

        history.IsPaused = false;
        Assert.True(await history.PollClipboardAsync());
        Assert.Single(history.FilteredClips);
        Assert.Equal("hello", history.FilteredClips[0].Content);
    }

    [Fact]
    public async Task TraySettings_DefaultOnAndPersist()
    {
        var app = new AppFixture();
        await using var _ = app;
        Assert.True(app.Settings.Current.CloseToTray);
        Assert.True(app.Settings.Current.MinimizeToTray);

        app.Main.Settings.CloseToTray = false;
        app.Main.Settings.MinimizeToTray = false;

        var reloaded = new SettingsStore(app.Settings.FilePath).Current;
        Assert.False(reloaded.CloseToTray);
        Assert.False(reloaded.MinimizeToTray);
    }

    [Fact]
    public async Task NotificationSetting_DefaultsOnAndPersists()
    {
        var app = new AppFixture();
        await using var _ = app;
        Assert.True(app.Settings.Current.Notifications);

        app.Main.Settings.Notifications = false;

        var reloaded = new SettingsStore(app.Settings.FilePath).Current;
        Assert.False(reloaded.Notifications);
    }

    [Fact]
    public async Task Settings_AcceptRemoteInput_TogglesAndPersists()
    {
        var app = new AppFixture();
        await using var _ = app;
        Assert.True(app.Settings.Current.AcceptRemoteInput);

        app.Main.Settings.AcceptRemoteInput = false;

        var reloaded = new SettingsStore(app.Settings.FilePath).Current;
        Assert.False(reloaded.AcceptRemoteInput);
    }

    [Fact]
    public void DeviceName_SettingPersistsAndFallbackToMachineName()
    {
        var app = new AppFixture();
        Assert.Equal(Environment.MachineName, app.Settings.Current.DeviceName);

        app.Main.Settings.DeviceName = "Laptop-Pro";
        var reloaded = new SettingsStore(app.Settings.FilePath).Current;
        Assert.Equal("Laptop-Pro", reloaded.DeviceName);

        app.Main.Settings.DeviceName = "   ";
        var emptyReloaded = new SettingsStore(app.Settings.FilePath).Current;
        Assert.Equal(Environment.MachineName, emptyReloaded.DeviceName);
    }

    [Fact]
    public void ToastService_WhenAppNullOrDisabled_DoesNotThrow()
    {
        ToastService.Show("Title", "Body");
        var store = new SettingsStore(Path.Combine(Path.GetTempPath(), "toast_test_" + Guid.NewGuid().ToString("N") + ".json"));
        store.Current.Notifications = false;
        ToastService.Initialize(store);
        ToastService.Show("Title", "Body");
        Assert.Empty(ToastService.ActiveToasts);
    }

    [Fact]
    public void Views_LoadTheirXaml()
    {
        Assert.NotNull(new MonitorLayoutView());
        Assert.NotNull(new ClipboardHistoryView());
        Assert.NotNull(new AndroidRemoteView());
        Assert.NotNull(new SettingsView());
    }

    [Fact]
    public void SingleInstance_FirstOwns_SecondSignalsAndReleases()
    {
        string testName = "Mouser_Test_" + Guid.NewGuid().ToString("N");
        using var first = new SingleInstance(testName);
        Assert.True(first.IsFirst);

        using var second = new SingleInstance(testName);
        Assert.False(second.IsFirst);
    }

    [Fact]
    public async Task ClipboardFilter_ByDevice()
    {
        var app = new AppFixture();
        await using var _ = app;
        var history = app.Main.ClipboardHistory;
        await history.InitializeAsync();

        await history.CaptureAsync("text 1");
        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "remote clip", SourceDevice = "OtherPC" });
        await history.ReloadAsync();

        Assert.Contains("OtherPC", history.DeviceOptions);

        history.SelectedDevice = "OtherPC";
        await history.ReloadAsync();
        Assert.Single(history.FilteredClips);
        Assert.Equal("remote clip", history.FilteredClips[0].Content);

        history.SelectedDevice = "All devices";
        await history.ReloadAsync();
        Assert.Equal(2, history.FilteredClips.Count);
    }

    [Fact]
    public async Task ClipboardFilter_ByTime()
    {
        var app = new AppFixture();
        await using var _ = app;
        var history = app.Main.ClipboardHistory;
        await history.InitializeAsync();

        await history.CaptureAsync("today clip");
        var tenDaysAgo = DateTimeOffset.UtcNow.AddDays(-10).ToUnixTimeMilliseconds();
        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "old clip", TimestampUtc = tenDaysAgo, SourceDevice = Environment.MachineName });
        await history.ReloadAsync();

        history.SelectedTime = "Today";
        await history.ReloadAsync();
        Assert.Single(history.FilteredClips);
        Assert.Equal("today clip", history.FilteredClips[0].Content);

        history.SelectedTime = "Yesterday";
        await history.ReloadAsync();
        Assert.Empty(history.FilteredClips);
        Assert.True(history.IsEmpty);

        history.SelectedTime = "Last 7 days";
        await history.ReloadAsync();
        Assert.Single(history.FilteredClips);
        Assert.Equal("today clip", history.FilteredClips[0].Content);

        history.SelectedTime = "Last 30 days";
        await history.ReloadAsync();
        Assert.Equal(2, history.FilteredClips.Count);
    }

    [Fact]
    public async Task ClipboardHistory_DetailPaneSelection()
    {
        await using var app = new AppFixture();
        var history = app.Main.ClipboardHistory;
        await history.InitializeAsync();
        await history.CaptureAsync("test detail clip");

        Assert.NotNull(history.SelectedClip);
        Assert.Equal("test detail clip", history.SelectedClip.Content);

        history.SelectedClip = null;
        Assert.Null(history.SelectedClip);
    }

    [Fact]
    public async Task DevicesViewModel_MergesOnlineAndOfflinePeers()
    {
        await using var app = new AppFixture();
        app.Settings.Current.Peers["offline-1"] = new PeerSettings { Name = "Desktop-Old" };
        var vm = new DevicesViewModel(app.Host, app.Settings, app.Main.AndroidRemote);

        Assert.Single(vm.Devices);
        Assert.Equal("Desktop-Old", vm.Devices[0].Name);
        Assert.False(vm.Devices[0].IsOnline);
    }

    [Fact]
    public async Task DevicesViewModel_ForgetsComputer()
    {
        await using var app = new AppFixture();
        app.Settings.Current.Peers["saved-1"] = new PeerSettings { Name = "Desktop-Saved", PairKeyProtected = SecretStore.Protect(new byte[32]) };
        var vm = new DevicesViewModel(app.Host, app.Settings, app.Main.AndroidRemote);

        Assert.Single(vm.Devices);
        var device = vm.Devices[0];
        Assert.Equal("saved-1", device.Id);

        vm.ForgetDeviceCommand.Execute(device);

        Assert.Empty(vm.Devices);
        Assert.False(app.Settings.Current.Peers.ContainsKey("saved-1"));
    }

    [Fact]
    public void ActivityLog_CapsAt200()
    {
        var log = new ActivityLog();
        for (int i = 0; i < 250; i++)
        {
            log.Record(ActivityCategory.Input, $"Event {i}");
        }

        var snapshot = log.Snapshot();
        Assert.Equal(200, snapshot.Count);
        Assert.Equal("Event 249", snapshot[0].Message);
    }

    [Fact]
    public async Task CopyDiagnostics_ContainsVersionAndRecentEvents()
    {
        await using var app = new AppFixture();
        app.Host.Log.Record(ActivityCategory.Discovery, "Found peer Desk (192.168.1.50)");

        await app.Main.Activity.CopyDiagnosticsCommand.ExecuteAsync(null);

        Assert.NotNull(app.Clipboard.Text);
        Assert.Contains("# Mouser Diagnostics", app.Clipboard.Text);
        Assert.Contains("Found peer Desk (192.168.1.50)", app.Clipboard.Text);
    }

    [Fact]
    public async Task Onboarding_NewStoreShowsWizard_NextAndDoneSavesAndCompletes()
    {
        await using var app = new AppFixture();
        string path = Path.Combine(Path.GetTempPath(), $"mouser_wizard_test_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(path);
            Assert.False(store.Current.FirstRunDone);

            var main = new MainViewModel(app.Host, store, app.Storage);
            Assert.NotNull(main.Onboarding);
            Assert.True(main.IsOnboarding);
            Assert.Equal(1, main.Onboarding.CurrentStep);

            // Step 1: Machine name -> Next
            main.Onboarding.DeviceName = "CustomDesk";
            main.Onboarding.NextCommand.Execute(null);
            Assert.Equal(2, main.Onboarding.CurrentStep);
            Assert.Equal("CustomDesk", store.Current.DeviceName);

            // Step 2: Toggles -> Next
            main.Onboarding.ClipboardSyncEnabled = false;
            main.Onboarding.KvmEnabled = true;
            main.Onboarding.AcceptRemoteInput = false;
            main.Onboarding.NextCommand.Execute(null);
            Assert.Equal(3, main.Onboarding.CurrentStep);
            Assert.False(store.Current.ClipboardSyncEnabled);
            Assert.True(store.Current.KvmEnabled);
            Assert.False(store.Current.AcceptRemoteInput);

            // Step 3: Pair a device -> Done
            Assert.Equal(0, main.Onboarding.DiscoveredDeviceCount);
            Assert.Equal("No devices discovered yet", main.Onboarding.DiscoveredDevicesText);

            main.Onboarding.DoneCommand.Execute(null);
            Assert.True(store.Current.FirstRunDone);
            Assert.Null(main.Onboarding);
            Assert.False(main.IsOnboarding);

            // Verify persisted to file
            var reloaded = new SettingsStore(path);
            Assert.True(reloaded.Current.FirstRunDone);
            Assert.Equal("CustomDesk", reloaded.Current.DeviceName);
            Assert.False(reloaded.Current.ClipboardSyncEnabled);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Onboarding_ExistingSettingsFile_DoesNotShowWizard()
    {
        await using var app = new AppFixture();
        string path = Path.Combine(Path.GetTempPath(), $"mouser_existing_test_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{\n  \"DeviceName\": \"ExistingPC\"\n}");
        try
        {
            var store = new SettingsStore(path);
            Assert.True(store.Current.FirstRunDone);

            var main = new MainViewModel(app.Host, store, app.Storage);
            Assert.Null(main.Onboarding);
            Assert.False(main.IsOnboarding);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Onboarding_SkipSetsFirstRunDoneAndDismisses()
    {
        await using var app = new AppFixture();
        string path = Path.Combine(Path.GetTempPath(), $"mouser_skip_test_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(path);
            Assert.False(store.Current.FirstRunDone);

            var main = new MainViewModel(app.Host, store, app.Storage);
            Assert.NotNull(main.Onboarding);
            Assert.True(main.IsOnboarding);

            main.Onboarding.SkipCommand.Execute(null);

            Assert.True(store.Current.FirstRunDone);
            Assert.Null(main.Onboarding);
            Assert.False(main.IsOnboarding);

            var reloaded = new SettingsStore(path);
            Assert.True(reloaded.Current.FirstRunDone);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Host_SetLocked_StopsAndRestartsServices()
    {
        await using var app = new AppFixture();
        var host = app.Host;
        Assert.NotNull(host.Input);
        Assert.NotNull(host.Discovery);

        host.SetLocked(true);
        Assert.True(host.IsLocked);
        Assert.Null(host.Input);
        Assert.Null(host.Discovery);

        host.Restart();
        Assert.Null(host.Input);
        Assert.Null(host.Discovery);

        host.SetLocked(false);
        Assert.False(host.IsLocked);
        Assert.NotNull(host.Input);
        Assert.NotNull(host.Discovery);
    }

    [Fact]
    public async Task ClipboardHistory_WhenLocked_PollClipboardDoesNothing()
    {
        await using var app = new AppFixture();
        await app.Main.ClipboardHistory.InitializeAsync();
        app.Main.ClipboardHistory.IsLocked = true;
        app.Clipboard.Text = "secret";

        bool captured = await app.Main.ClipboardHistory.PollClipboardAsync();
        Assert.False(captured);
        Assert.Empty(app.Main.ClipboardHistory.History);
        Assert.True(app.Main.ClipboardHistory.History.Count == 0);
    }

    [Fact]
    public async Task LockScreen_Unlock_WrongPassphraseShowsError_RightPassphraseUnlocks()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"mouser_lock_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string lockFile = Path.Combine(dir, "lock.json");
            var lockService = new AppLockService(lockFile, null, 1000);
            lockService.Setup("SuperSecret123!");
            lockService.Lock();

            await using var app = new AppFixture(appLock: lockService);
            Assert.True(app.Main.IsLocked);
            Assert.NotNull(app.Main.Lock);
            Assert.Null(app.Host.Input);
            Assert.True(app.Main.ClipboardHistory.IsLocked);

            // Wrong passphrase
            app.Main.Lock.Passphrase = "WrongPassphrase1";
            app.Main.Lock.UnlockCommand.Execute(null);

            Assert.True(app.Main.IsLocked);
            Assert.NotNull(app.Main.Lock.Error);
            Assert.Equal("That passphrase is wrong.", app.Main.Lock.Error);
            Assert.Equal(string.Empty, app.Main.Lock.Passphrase);
            Assert.Null(app.Host.Input);

            // Right passphrase
            app.Main.Lock.Passphrase = "SuperSecret123!";
            app.Main.Lock.UnlockCommand.Execute(null);

            Assert.False(app.Main.IsLocked);
            Assert.Null(app.Main.Lock.Error);
            Assert.NotNull(app.Host.Input);
            Assert.False(app.Main.ClipboardHistory.IsLocked);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task LockScreen_LockCommand_StopsHostInput()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"mouser_lock_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string lockFile = Path.Combine(dir, "lock.json");
            var lockService = new AppLockService(lockFile, null, 1000);
            lockService.Setup("SuperSecret123!");

            await using var app = new AppFixture(appLock: lockService);
            Assert.False(app.Main.IsLocked);
            Assert.NotNull(app.Host.Input);

            lockService.Lock();

            Assert.True(app.Main.IsLocked);
            Assert.Null(app.Host.Input);
            Assert.True(app.Main.ClipboardHistory.IsLocked);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task LockScreen_RecoveryKey_UnlocksSuccessfully()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"mouser_lock_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string lockFile = Path.Combine(dir, "lock.json");
            var lockService = new AppLockService(lockFile, null, 1000);
            string recKey = lockService.Setup("SuperSecret123!");
            lockService.Lock();

            await using var app = new AppFixture(appLock: lockService);
            Assert.True(app.Main.IsLocked);
            Assert.NotNull(app.Main.Lock);

            app.Main.Lock.ToggleRecoveryBoxCommand.Execute(null);
            Assert.True(app.Main.Lock.ShowRecoveryBox);

            // Wrong recovery key
            app.Main.Lock.RecoveryKey = "INVALID-RECOVERY-KEY";
            app.Main.Lock.UseRecoveryKeyCommand.Execute(null);

            Assert.True(app.Main.IsLocked);
            Assert.Equal("That recovery key is wrong.", app.Main.Lock.Error);

            // Valid recovery key
            app.Main.Lock.RecoveryKey = recKey;
            app.Main.Lock.UseRecoveryKeyCommand.Execute(null);

            Assert.False(app.Main.IsLocked);
            Assert.Null(app.Main.Lock.Error);
            Assert.False(app.Main.Lock.ShowRecoveryBox);
            Assert.NotNull(app.Host.Input);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task LockScreen_Backoff_ShowsRemainingSeconds()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"mouser_lock_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        DateTime fakeTime = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        try
        {
            string lockFile = Path.Combine(dir, "lock.json");
            var lockService = new AppLockService(lockFile, () => fakeTime, 1000);
            lockService.Setup("SuperSecret123!");
            lockService.Lock();

            await using var app = new AppFixture(appLock: lockService);

            for (int i = 0; i < 3; i++)
            {
                app.Main.Lock!.Passphrase = "wrong";
                app.Main.Lock.UnlockCommand.Execute(null);
                Assert.Equal("That passphrase is wrong.", app.Main.Lock.Error);
                Assert.False(app.Main.Lock.IsBackingOff);
            }

            // 4th failure triggers 5 second backoff
            app.Main.Lock!.Passphrase = "wrong";
            app.Main.Lock.UnlockCommand.Execute(null);
            Assert.True(app.Main.Lock.IsBackingOff);
            Assert.Contains("Too many tries. Wait", app.Main.Lock.Error);

            // Immediate attempt during backoff
            app.Main.Lock.Passphrase = "SuperSecret123!";
            app.Main.Lock.UnlockCommand.Execute(null);
            Assert.True(app.Main.IsLocked);
            Assert.Contains("Too many tries. Wait", app.Main.Lock.Error);

            // Advance clock past backoff
            fakeTime = fakeTime.AddSeconds(6);
            app.Main.Lock.Passphrase = "SuperSecret123!";
            app.Main.Lock.UnlockCommand.Execute(null);
            Assert.False(app.Main.IsLocked);
            Assert.Null(app.Main.Lock.Error);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task SettingsViewModel_Security_FullLifecycle()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"mouser_lock_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string lockFile = Path.Combine(dir, "lock.json");
            var lockService = new AppLockService(lockFile, null, 1000);
            await using var app = new AppFixture(appLock: lockService);

            var s = app.Main.Settings;
            Assert.False(s.IsLockConfigured);

            s.StartSetupPassphraseCommand.Execute(null);
            Assert.True(s.IsSettingPassphrase);

            // Too short
            s.NewPassphrase = "short";
            s.ConfirmPassphrase = "short";
            s.ConfirmSetupPassphraseCommand.Execute(null);
            Assert.Equal("Passphrase must be at least 8 characters.", s.SetupError);

            // Mismatch
            s.NewPassphrase = "password123";
            s.ConfirmPassphrase = "password456";
            s.ConfirmSetupPassphraseCommand.Execute(null);
            Assert.Equal("Passphrases do not match.", s.SetupError);

            // Valid
            s.NewPassphrase = "password123";
            s.ConfirmPassphrase = "password123";
            s.ConfirmSetupPassphraseCommand.Execute(null);
            Assert.True(s.ShowRecoveryKeyPrompt);
            Assert.False(string.IsNullOrEmpty(s.SavedRecoveryKey));

            // Acknowledge recovery key
            s.AcknowledgeRecoveryKeyCommand.Execute(null);
            Assert.False(s.ShowRecoveryKeyPrompt);
            Assert.False(s.IsSettingPassphrase);
            Assert.True(s.IsLockConfigured);

            // Lock now
            s.LockNowCommand.Execute(null);
            Assert.True(app.Main.IsLocked);
            Assert.Null(app.Host.Input);

            // Unlock
            app.Main.Lock!.Passphrase = "password123";
            app.Main.Lock.UnlockCommand.Execute(null);
            Assert.False(app.Main.IsLocked);
            Assert.NotNull(app.Host.Input);

            // Change passphrase
            s.StartChangePassphraseCommand.Execute(null);
            Assert.True(s.IsChangingPassphrase);
            s.OldPassphrase = "wrongpassword";
            s.ChangeNewPassphrase = "newpassword456";
            s.ChangeConfirmPassphrase = "newpassword456";
            s.ChangePassphraseCommand.Execute(null);
            Assert.Equal("Incorrect current passphrase.", s.ChangeError);

            s.OldPassphrase = "password123";
            s.ChangePassphraseCommand.Execute(null);
            Assert.False(s.IsChangingPassphrase);
            Assert.Null(s.ChangeError);

            // Remove lock
            s.StartRemoveLockCommand.Execute(null);
            Assert.True(s.IsRemovingLock);
            s.RemovePassphrase = "wrongpassword";
            s.RemoveLockCommand.Execute(null);
            Assert.Equal("Incorrect passphrase.", s.RemoveError);

            s.RemovePassphrase = "newpassword456";
            s.RemoveLockCommand.Execute(null);
            Assert.False(s.IsRemovingLock);
            Assert.False(s.IsLockConfigured);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task SettingsViewModel_AutoLockAndWindowsLock_Persisted()
    {
        await using var app = new AppFixture();
        var s = app.Main.Settings;

        s.LockWithWindows = false;
        Assert.False(app.Settings.Current.LockWithWindows);

        s.AutoLockMinutes = 15;
        Assert.Equal(15, app.Settings.Current.AutoLockMinutes);
    }

    [Fact]
    public async Task SettingsViewModel_SwitchFeel_Persisted()
    {
        await using var app = new AppFixture();
        var s = app.Main.Settings;

        Assert.Equal("Normal", s.SwitchFeel);
        Assert.True(s.IsSwitchFeelNormal);
        Assert.False(s.IsSwitchFeelRelaxed);
        Assert.False(s.IsSwitchFeelCareful);

        s.IsSwitchFeelRelaxed = true;
        Assert.Equal("Relaxed", s.SwitchFeel);
        Assert.Equal("Relaxed", app.Settings.Current.SwitchFeel);
        Assert.True(s.IsSwitchFeelRelaxed);
        Assert.False(s.IsSwitchFeelNormal);
        Assert.False(s.IsSwitchFeelCareful);

        s.IsSwitchFeelCareful = true;
        Assert.Equal("Careful", s.SwitchFeel);
        Assert.Equal("Careful", app.Settings.Current.SwitchFeel);
        Assert.True(s.IsSwitchFeelCareful);
        Assert.False(s.IsSwitchFeelRelaxed);
        Assert.False(s.IsSwitchFeelNormal);
    }

    [Fact]
    public void SettingsStore_SwitchFeel_Validation()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"mouser_feel_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "settings.json");
            File.WriteAllText(file, "{\"SwitchFeel\": \"InvalidFeel\"}");
            var store = new SettingsStore(file);
            Assert.Equal("Normal", store.Current.SwitchFeel);

            File.WriteAllText(file, "{\"SwitchFeel\": \"Relaxed\"}");
            var storeRelaxed = new SettingsStore(file);
            Assert.Equal("Relaxed", storeRelaxed.Current.SwitchFeel);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task App_StartsLocked_ServicesStayStopped()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"mouser_lock_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string lockFile = Path.Combine(dir, "lock.json");
            var lock1 = new AppLockService(lockFile, null, 1000);
            lock1.Setup("StartLocked123!");

            // Reconstruct as if starting the app anew
            var lock2 = new AppLockService(lockFile, null, 1000);
            Assert.True(lock2.IsLocked);

            await using var app = new AppFixture(appLock: lock2);
            Assert.True(app.Main.IsLocked);
            Assert.Null(app.Host.Input);
            Assert.Null(app.Host.Discovery);
            Assert.True(app.Main.ClipboardHistory.IsLocked);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task CtrlL_LocksAppWhenConfigured()
    {
        // When unconfigured: does not throw, does not lock
        await using var app1 = new AppFixture();
        Assert.False(app1.Main.CanLockNow);
        app1.Main.LockNowCommand.Execute(null);
        Assert.False(app1.Main.IsLocked);

        // When configured: locks the app and updates CanLockNow
        string dir = Path.Combine(Path.GetTempPath(), $"mouser_lock_ctrl_l_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string lockFile = Path.Combine(dir, "lock.json");
            var lockService = new AppLockService(lockFile, null, 1000);
            lockService.Setup("Passphrase123!");
            Assert.False(lockService.IsLocked);

            await using var app2 = new AppFixture(appLock: lockService);
            Assert.False(app2.Main.IsLocked);
            Assert.True(app2.Main.CanLockNow);

            app2.Main.LockNowCommand.Execute(null);
            Assert.True(app2.Main.IsLocked);
            Assert.False(app2.Main.CanLockNow);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void WindowsSession_MethodsDoNotThrow()
    {
        bool locked = WindowsSession.IsWorkstationLocked();
        Assert.True(locked || !locked);

        TimeSpan idle = WindowsSession.IdleTime();
        Assert.True(idle >= TimeSpan.Zero);
    }

    [Fact]
    public void QuitWhileLocked_BlockedUntilUnlocked()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"mouser_lock_quit_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string lockFile = Path.Combine(dir, "lock.json");
            var lockService = new AppLockService(lockFile, null, 1000);
            lockService.Setup("Password123!");

            var app = new App();
            app.SetAppLockForTesting(lockService);

            // When not locked: quit succeeds immediately
            Assert.True(app.RequestQuit());
            Assert.True(app.Quitting);

            // Re-test with locked app
            var app2 = new App();
            lockService.Lock();
            Assert.True(lockService.IsLocked);
            app2.SetAppLockForTesting(lockService);

            // Quit attempt while locked is blocked
            bool quitResult = app2.RequestQuit();
            Assert.False(quitResult);
            Assert.True(app2.QuitAfterUnlock);
            Assert.False(app2.Quitting);

            // Unlocking allows the quit to proceed
            bool unlocked = lockService.TryUnlock("Password123!");
            Assert.True(unlocked);
            Assert.True(app2.Quitting);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task QuickPaste_BlockedWhileLocked()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"mouser_lock_quickpaste_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string lockFile = Path.Combine(dir, "lock.json");
            var lockService = new AppLockService(lockFile, null, 1000);
            lockService.Setup("Password123!");

            await using var fixture = new AppFixture(appLock: lockService);

            var app = new App();
            app.SetAppLockForTesting(lockService);

            // When unlocked: service is not locked
            Assert.False(lockService.IsLocked);

            // Now lock the service
            lockService.Lock();
            Assert.True(lockService.IsLocked);

            // When locked: ShowQuickPaste immediately returns false
            Assert.False(app.ShowQuickPaste(0, fixture.Main));

            // Also verify clipboard polling is blocked
            bool pollResult = await fixture.Main.ClipboardHistory.PollClipboardAsync();
            Assert.False(pollResult);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void AutoLock_TriggerLogic()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"mouser_lock_trigger_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string lockFile = Path.Combine(dir, "lock.json");
            var unconfigured = new AppLockService(Path.Combine(dir, "nonexistent.json"));
            var settings = new AppSettings { LockWithWindows = true, AutoLockMinutes = 5 };

            // Unconfigured lock never triggers
            Assert.False(App.ShouldAutoLock(unconfigured, settings, isWorkstationLocked: true, TimeSpan.FromMinutes(10)));
            Assert.False(App.ShouldAutoLock(null, settings, isWorkstationLocked: true, TimeSpan.FromMinutes(10)));

            var configured = new AppLockService(lockFile, null, 1000);
            configured.Setup("Password123!");

            // Workstation locked triggers when LockWithWindows is true
            Assert.True(App.ShouldAutoLock(configured, settings, isWorkstationLocked: true, TimeSpan.Zero));

            // Workstation locked does not trigger when LockWithWindows is false
            var settingsNoWin = new AppSettings { LockWithWindows = false, AutoLockMinutes = 0 };
            Assert.False(App.ShouldAutoLock(configured, settingsNoWin, isWorkstationLocked: true, TimeSpan.Zero));

            // Idle timer triggers when idle >= AutoLockMinutes
            var settingsIdle = new AppSettings { LockWithWindows = false, AutoLockMinutes = 10 };
            Assert.False(App.ShouldAutoLock(configured, settingsIdle, isWorkstationLocked: false, TimeSpan.FromMinutes(9)));
            Assert.True(App.ShouldAutoLock(configured, settingsIdle, isWorkstationLocked: false, TimeSpan.FromMinutes(10)));
            Assert.True(App.ShouldAutoLock(configured, settingsIdle, isWorkstationLocked: false, TimeSpan.FromMinutes(15)));

            // Idle timer disabled (0) never triggers
            var settingsIdleOff = new AppSettings { LockWithWindows = false, AutoLockMinutes = 0 };
            Assert.False(App.ShouldAutoLock(configured, settingsIdleOff, isWorkstationLocked: false, TimeSpan.FromHours(5)));

            // Already locked service does not re-trigger
            configured.Lock();
            Assert.True(configured.IsLocked);
            Assert.False(App.ShouldAutoLock(configured, settings, isWorkstationLocked: true, TimeSpan.FromMinutes(10)));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
