using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mouser.Core.Clipboard;
using Mouser.Core.Kvm;
using Mouser.Core.Networking;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public class ClipboardSyncRulesTests
{
    private static async Task<(AppFixture A, AppFixture B)> PairedAsync()
    {
        int discovery = Random.Shared.Next(40_000, 50_000);
        var a = new AppFixture(discovery);
        var b = new AppFixture(discovery);
        await Wait.UntilAsync(() => a.Host.Peers.Any(p => p.DeviceId == b.Settings.Current.DeviceId)
                                    && b.Host.Peers.Any(p => p.DeviceId == a.Settings.Current.DeviceId), timeoutMs: 5000);

        var peerB = a.Host.Peers.Single(p => p.DeviceId == b.Settings.Current.DeviceId);
        Assert.True(await a.Host.PairPeerAsync(peerB, b.Settings.Current.PairingPin));
        return (a, b);
    }

    [Fact]
    public async Task ShareClipboardAsync_WhenSendClipboardFalse_ReturnsSkipped()
    {
        var (a, b) = await PairedAsync();
        await using var _a = a;
        await using var _b = b;

        string bId = b.Settings.Current.DeviceId;
        a.Settings.Current.Peers[bId].SendClipboard = false;
        a.Settings.Save();

        bool received = false;
        b.Host.ClipReceived += (_, _) => received = true;

        var deliveries = await a.Host.ShareClipboardAsync("test text");
        await Task.Delay(300);

        Assert.False(received);
        var delivery = Assert.Single(deliveries, d => d.DeviceId == bId);
        Assert.Equal("Skipped", delivery.Status);
        Assert.Equal("Clipboard sending disabled for this device", delivery.Error);
    }

    [Fact]
    public async Task IncomingClip_WhenReceiveClipboardFalse_DropsClip()
    {
        var (a, b) = await PairedAsync();
        await using var _a = a;
        await using var _b = b;

        string aId = a.Settings.Current.DeviceId;
        if (!b.Settings.Current.Peers.TryGetValue(aId, out var peerSettings))
        {
            b.Settings.Current.Peers[aId] = peerSettings = new PeerSettings { Name = "A" };
        }
        peerSettings.ReceiveClipboard = false;
        b.Settings.Save();

        bool receivedAtB = false;
        b.Host.ClipReceived += (_, _) => receivedAtB = true;

        var deliveries = await a.Host.ShareClipboardAsync("should be dropped by B");
        await Task.Delay(300);

        Assert.False(receivedAtB);
    }

    [Fact]
    public async Task SendClipToAsync_SendsEvenWhenSendClipboardFalse()
    {
        var (a, b) = await PairedAsync();
        await using var _a = a;
        await using var _b = b;

        string bId = b.Settings.Current.DeviceId;
        a.Settings.Current.Peers[bId].SendClipboard = false;
        a.Settings.Save();

        var receivedTcs = new TaskCompletionSource<string>();
        b.Host.ClipReceived += (_, clip) => receivedTcs.TrySetResult(clip.TextContent);

        var peerB = a.Host.Peers.Single(p => p.DeviceId == bId);
        var delivery = await a.Host.SendClipToAsync(peerB, "manual push text");

        Assert.Equal("Sent", delivery.Status);
        Assert.Null(delivery.Error);
        Assert.Equal("manual push text", await receivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void PeerSettings_Serialization_PreservesSyncRules()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mouser_syncrules_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var store1 = new SettingsStore(Path.Combine(tempDir, "settings.json"));
            store1.Current.Peers["peer-test"] = new PeerSettings
            {
                Name = "Test Peer",
                Pin = 123456,
                Placement = PeerPlacement.Left,
                SendClipboard = false,
                ReceiveClipboard = false,
                SendFiles = false
            };
            store1.Save();

            var store2 = new SettingsStore(Path.Combine(tempDir, "settings.json"));
            Assert.True(store2.Current.Peers.TryGetValue("peer-test", out var loaded));
            Assert.Equal("Test Peer", loaded.Name);
            Assert.Equal(123456, loaded.Pin);
            Assert.Equal(PeerPlacement.Left, loaded.Placement);
            Assert.False(loaded.SendClipboard);
            Assert.False(loaded.ReceiveClipboard);
            Assert.False(loaded.SendFiles);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ClipboardHistory_SendClipToPeer_UpdatesDeliveriesAndStatus()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mouser_vm_sendto_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "clipboard.db");
        var settingsPath = Path.Combine(tempDir, "settings.json");
        var storage = new ClipboardStorageService(dbPath);
        var settings = new SettingsStore(settingsPath);
        try
        {
            await storage.InitializeAsync();
            var vm = new ClipboardHistoryViewModel(storage, settings);
            await vm.InitializeAsync();

            var clip = new ClipboardPayload { TextContent = "Clip to push" };
            await storage.SaveClipAsync(clip);
            await vm.ReloadAsync();

            Assert.NotNull(vm.SelectedClip);
            Assert.Equal("Clip to push", vm.SelectedClip.Content);

            var peer = new DiscoveredPeer
            {
                DeviceId = "dev-target-1",
                Hostname = "TargetPC",
                ControlPort = 9999
            };

            vm.SendClipToPeerHandler = (p, text) =>
            {
                Assert.Equal("dev-target-1", p.DeviceId);
                Assert.Equal("Clip to push", text);
                return Task.FromResult(new ClipDelivery(p.DeviceId, p.Hostname, "Sent", null));
            };

            await vm.SendClipToPeerCommand.ExecuteAsync(peer);

            Assert.Equal($"Sent clip to {peer.DeviceName}.", vm.StatusMessage);
            Assert.True(vm.HasDeliveries);
            var delivery = Assert.Single(vm.SelectedClipDeliveries);
            Assert.Equal("TargetPC", delivery.DeviceName);
            Assert.Equal("Sent", delivery.Status);

            var inDb = await storage.GetDeliveriesAsync(vm.SelectedClip.Id);
            var dbDelivery = Assert.Single(inDb);
            Assert.Equal("dev-target-1", dbDelivery.DeviceId);
            Assert.Equal("Sent", dbDelivery.Status);
        }
        finally
        {
            await storage.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void DeviceDetailViewModel_TogglingSyncRules_SavesToSettings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mouser_devicedetail_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var settings = new SettingsStore(Path.Combine(tempDir, "settings.json"));
            var detail = new DeviceDetailViewModel("dev-1", DeviceKind.Computer, settings)
            {
                Name = "Workstation"
            };

            Assert.True(detail.SendClipboard);
            Assert.True(detail.ReceiveClipboard);
            Assert.True(detail.SendFiles);

            detail.SendClipboard = false;
            detail.ReceiveClipboard = false;
            detail.SendFiles = false;

            Assert.False(settings.Current.Peers["dev-1"].SendClipboard);
            Assert.False(settings.Current.Peers["dev-1"].ReceiveClipboard);
            Assert.False(settings.Current.Peers["dev-1"].SendFiles);

            var reloadedSettings = new SettingsStore(Path.Combine(tempDir, "settings.json"));
            Assert.False(reloadedSettings.Current.Peers["dev-1"].SendClipboard);
            Assert.False(reloadedSettings.Current.Peers["dev-1"].ReceiveClipboard);
            Assert.False(reloadedSettings.Current.Peers["dev-1"].SendFiles);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
