using System.Net;
using Mouser.Core.Clipboard;
using Mouser.Core.Input;
using Mouser.Core.Networking;
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

        Assert.True(vm.IsRemoteTab);
        Assert.IsType<AndroidRemoteViewModel>(vm.CurrentPage);

        vm.NavigateCommand.Execute("SmartClipboard");
        Assert.True(vm.IsClipboardTab);
        Assert.False(vm.IsRemoteTab);
        Assert.IsType<ClipboardHistoryViewModel>(vm.CurrentPage);

        vm.NavigateCommand.Execute("DisplayMatrix");
        Assert.IsType<MonitorLayoutViewModel>(vm.CurrentPage);

        vm.NavigateCommand.Execute("Settings");
        Assert.IsType<SettingsViewModel>(vm.CurrentPage);
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
    public void Views_LoadTheirXaml()
    {
        Assert.NotNull(new MonitorLayoutView());
        Assert.NotNull(new ClipboardHistoryView());
        Assert.NotNull(new AndroidRemoteView());
        Assert.NotNull(new SettingsView());
    }
}
