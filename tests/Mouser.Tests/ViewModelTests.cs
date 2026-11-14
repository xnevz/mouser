using System.Net;
using Mouser.Core.Clipboard;
using Mouser.Core.Input;
using Mouser.Core.Networking;
using Mouser.Core.Services;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Mouser.UI.Views;

namespace Mouser.Tests;

public sealed class FakeClipboard : IClipboardAccess
{
    public string? Text { get; set; }
    public Task<string?> GetTextAsync() => Task.FromResult(Text);
    public Task SetTextAsync(string text) { Text = text; return Task.CompletedTask; }
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
    public MainViewModel Main { get; }

    public string Downloads => Path.Combine(_dir, "downloads");

    public AppFixture(int? discoveryPort = null)
    {
        Settings = new SettingsStore(Path.Combine(_dir, "settings.json"));
        Settings.Current.InputPort = 0; // any free port
        Host = new MouserHost(Settings, Sink, discoveryPort: discoveryPort ?? Random.Shared.Next(40_000, 50_000),
            downloadDirectory: Path.Combine(_dir, "downloads"));
        Host.Restart();
        Storage = new ClipboardStorageService(Path.Combine(_dir, "clipboard.db"));
        Main = new MainViewModel(Host, Settings, Storage);
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
    public async Task PhoneRemote_ShowsPinAndListsAPairedPhone()
    {
        await using var app = new AppFixture();
        var remote = app.Main.AndroidRemote;
        Assert.Equal(PairingGate.FormatPin(app.Settings.Current.PairingPin), remote.PairingPinText);
        Assert.True(remote.IsAccepting);

        using var phone = new FakePhone(app.Host.Input!.BoundPort);
        Assert.True(await phone.PairAsync(app.Settings.Current.PairingPin));
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
        Assert.True(await phone.PairAsync(app.Settings.Current.PairingPin));
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
    public async Task PhoneRemote_NewPinUnpairsPhonesAndIsSaved()
    {
        await using var app = new AppFixture();
        int oldPin = app.Settings.Current.PairingPin;
        app.Host.Pairing.TryPair(IPAddress.Loopback, oldPin);

        app.Main.AndroidRemote.RegeneratePinCommand.Execute(null);

        Assert.NotEqual(oldPin, app.Settings.Current.PairingPin);
        Assert.False(app.Host.Pairing.IsTrusted(IPAddress.Loopback));
        var reloaded = new SettingsStore(app.Settings.FilePath);
        Assert.Equal(app.Settings.Current.PairingPin, reloaded.Current.PairingPin);
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
            Assert.InRange(store.Current.PairingPin, 0, 999_999);
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
    public async Task DevicesViewModel_PairsComputer()
    {
        await using var app = new AppFixture();
        var vm = new DevicesViewModel(app.Host, app.Settings, app.Main.AndroidRemote);
        var device = new DeviceDetailViewModel("peer-1", DeviceKind.Computer)
        {
            Name = "Laptop-1",
            PinText = "123456"
        };
        vm.Devices.Add(device);

        await vm.PairComputerCommand.ExecuteAsync(device);

        Assert.True(device.IsPaired);
        Assert.True(app.Settings.Current.Peers.ContainsKey("peer-1"));
        Assert.Equal(123456, app.Settings.Current.Peers["peer-1"].Pin);
    }

    [Fact]
    public async Task DevicesViewModel_ForgetsComputer()
    {
        await using var app = new AppFixture();
        app.Settings.Current.Peers["saved-1"] = new PeerSettings { Name = "Desktop-Saved", Pin = 654321 };
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
            Assert.NotEmpty(main.Onboarding.PairingPinText);
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
}
