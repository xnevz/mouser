using Mouser.Core.Clipboard;
using Mouser.Core.Geometry;
using Mouser.Core.Input;
using Mouser.Core.Kvm;
using Mouser.UI.Services;

namespace Mouser.Tests;

/// <summary>Two complete desktop apps on one machine, finding and pairing with each other over real sockets.</summary>
public class TwoComputerTests
{
    private static async Task<(AppFixture A, AppFixture B)> PairedAsync()
    {
        int discovery = Random.Shared.Next(40_000, 50_000);
        var a = new AppFixture(discovery);
        var b = new AppFixture(discovery);
        b.Host.AllowNewDevices(TimeSpan.FromMinutes(2));
        b.Host.PairingRequested += (_, _, allow, _) => allow();
        // Each side learns the other from its once-a-second beacon; wait until both have.
        await Wait.UntilAsync(() => a.Host.Peers.Any(p => p.DeviceId == b.Settings.Current.DeviceId)
                                    && b.Host.Peers.Any(p => p.DeviceId == a.Settings.Current.DeviceId), timeoutMs: 5000);

        var peerB = a.Host.Peers.Single(p => p.DeviceId == b.Settings.Current.DeviceId);
        Assert.True(await a.Host.PairPeerAsync(peerB));
        return (a, b);
    }

    [Fact]
    public async Task PairingSavesThePeer_PlacesItRight_AndReportsItPaired()
    {
        var (a, b) = await PairedAsync();
        await using var _a = a;
        await using var _b = b;

        var saved = a.Settings.Current.Peers[b.Settings.Current.DeviceId];
        Assert.False(string.IsNullOrEmpty(saved.PairKeyProtected));
        Assert.Equal(PeerPlacement.Right, saved.Placement);
        Assert.Equal(PeerLinkStatus.Paired, a.Host.GetPeerStatus(b.Settings.Current.DeviceId));
    }

    [Fact]
    public async Task ClipboardSyncs_InBothDirections_FromOnePairing()
    {
        var (a, b) = await PairedAsync();
        await using var _a = a;
        await using var _b = b;
        var atB = new TaskCompletionSource<string>();
        var atA = new TaskCompletionSource<string>();
        b.Host.ClipReceived += (_, clip) => atB.TrySetResult(clip.TextContent);
        a.Host.ClipReceived += (_, clip) => atA.TrySetResult(clip.TextContent);

        await a.Host.ShareClipboardAsync("copied on A");
        Assert.Equal("copied on A", await atB.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        // B never entered A's PIN, but the pairing key works both ways.
        await b.Host.ShareClipboardAsync("copied on B");
        Assert.Equal("copied on B", await atA.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ReceivedClip_LandsOnTheClipboardAndInHistory_WithoutEchoingBack()
    {
        var (a, b) = await PairedAsync();
        await using var _a = a;
        await using var _b = b;
        await b.Main.ClipboardHistory.InitializeAsync();
        int sentBackToA = 0;
        a.Host.ClipReceived += (_, _) => sentBackToA++;
        b.Main.ClipboardHistory.ShareCapturedText = b.Host.ShareClipboardAsync;

        await b.Main.ClipboardHistory.ApplyRemoteClipAsync("A-PC", new ClipboardPayload { TextContent = "from A" });
        await b.Main.ClipboardHistory.PollClipboardAsync(); // B's poll sees its own write

        Assert.Equal("from A", b.Clipboard.Text);
        Assert.Equal("A-PC", Assert.Single(b.Main.ClipboardHistory.FilteredClips).SourceDevice);
        await Task.Delay(300);
        Assert.Equal(0, sentBackToA);
    }

    [Fact]
    public async Task SyncOff_StopsSendingAndReceiving()
    {
        var (a, b) = await PairedAsync();
        await using var _a = a;
        await using var _b = b;
        bool received = false;
        b.Host.ClipReceived += (_, _) => received = true;

        b.Settings.Current.ClipboardSyncEnabled = false;
        await a.Host.ShareClipboardAsync("should be ignored by B");
        await Task.Delay(300);

        Assert.False(received);
    }

    [Fact]
    public async Task FileSentToPeer_ArrivesInItsDownloadFolder()
    {
        var (a, b) = await PairedAsync();
        await using var _a = a;
        await using var _b = b;
        string source = Path.Combine(Path.GetTempPath(), $"mouser_doc_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(source, "quarterly numbers");
        var arrived = new TaskCompletionSource<string>();
        b.Host.FileReceived += (_, path) => arrived.TrySetResult(path);
        try
        {
            var peerB = a.Host.Peers.Single(p => p.DeviceId == b.Settings.Current.DeviceId);
            Assert.True(a.Host.CanSendFileTo(peerB));
            await a.Host.SendFileAsync(peerB, source);

            string path = await arrived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.StartsWith(b.Downloads, path);
            Assert.Equal("quarterly numbers", await File.ReadAllTextAsync(path));
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public async Task UnpairedComputers_CannotSyncOrSendFiles()
    {
        int discovery = Random.Shared.Next(40_000, 50_000);
        await using var a = new AppFixture(discovery);
        await using var b = new AppFixture(discovery);
        await Wait.UntilAsync(() => a.Host.Peers.Any(p => p.DeviceId == b.Settings.Current.DeviceId), timeoutMs: 5000);
        bool received = false;
        b.Host.ClipReceived += (_, _) => received = true;

        await a.Host.ShareClipboardAsync("secret");
        await Task.Delay(300);

        Assert.False(received);
        Assert.False(a.Host.CanSendFileTo(a.Host.Peers.Single()));
    }

    [Fact]
    public async Task AddByIp_AcrossDifferentDiscoveryPorts_DiscoversAndPairs()
    {
        int discA = Random.Shared.Next(40_000, 45_000);
        int discB = Random.Shared.Next(45_001, 50_000);
        await using var a = new AppFixture(discA);
        await using var b = new AppFixture(discB);

        // Discovery ports differ, so neither discovers the other via beacon
        await Task.Delay(200);
        Assert.DoesNotContain(a.Host.Peers, p => p.DeviceId == b.Settings.Current.DeviceId);

        b.Host.AllowNewDevices(TimeSpan.FromMinutes(2));
        b.Host.PairingRequested += (_, _, allow, _) => allow();

        // Add B by IP on A
        a.Main.DevicesPage.ManualAddress = $"127.0.0.1:{b.Host.DiscoveryPort}";
        a.Main.DevicesPage.AddByAddressCommand.Execute(null);

        await Wait.UntilAsync(() => a.Host.Peers.Any(p => p.DeviceId == b.Settings.Current.DeviceId), timeoutMs: 10_000);

        var peerB = a.Host.Peers.Single(p => p.DeviceId == b.Settings.Current.DeviceId);
        bool paired = await a.Host.PairPeerAsync(peerB);
        Assert.True(paired);
        Assert.Equal(PeerLinkStatus.Paired, a.Host.GetPeerStatus(b.Settings.Current.DeviceId));
    }

    [Fact]
    public async Task KvmCrossing_MovesInputFromLocalToRemotePeer()
    {
        var capture = new FakeCapture();
        int discovery = Random.Shared.Next(40_000, 50_000);
        await using var a = new AppFixture(discovery, capture: capture);
        await using var b = new AppFixture(discovery);
        b.Host.AllowNewDevices(TimeSpan.FromMinutes(2));
        b.Host.PairingRequested += (_, _, allow, _) => allow();

        await Wait.UntilAsync(() => a.Host.Peers.Any(p => p.DeviceId == b.Settings.Current.DeviceId)
                                    && b.Host.Peers.Any(p => p.DeviceId == a.Settings.Current.DeviceId), timeoutMs: 5000);

        var peerB = a.Host.Peers.Single(p => p.DeviceId == b.Settings.Current.DeviceId);
        Assert.True(await a.Host.PairPeerAsync(peerB));

        // Configure screens for KVM transition: local at 0,0, 1920x1080; B placed at Right
        var desktop = new ScreenBounds { X = 0, Y = 0, Width = 1920, Height = 1080 };
        a.Host.SetLocalDesktop(desktop);
        var link = a.Host.EnsureLink(peerB);
        a.Host.Kvm!.SetLocalScreen(desktop);
        a.Host.Kvm.SetPeer(b.Settings.Current.DeviceId, "b", link, desktop, PeerPlacement.Right);
        a.Host.Kvm.Start();

        // Push against right edge to trigger transition to B
        Assert.True(capture.Move(1925, 540, 30, 0));
        Assert.Equal(b.Settings.Current.DeviceId, a.Host.Kvm.ActivePeerId);

        // Send a click and key
        capture.Button(MouseButton.Left, true);
        capture.Button(MouseButton.Left, false);
        capture.Key(0x41, true); // 'A' key
        capture.Key(0x41, false);

        await Wait.UntilAsync(() => b.Sink.Snapshot().Length >= 2, timeoutMs: 5000);
        var snapshot = b.Sink.Snapshot();
        Assert.Contains(snapshot, s => s.Contains("Left down", StringComparison.OrdinalIgnoreCase) || s.Contains("left down") || s.Contains("0x41"));
    }

    [Fact]
    public async Task ClipboardImageSync_TransfersImagePayload_BetweenPairedComputers()
    {
        var (a, b) = await PairedAsync();
        await using var _a = a;
        await using var _b = b;

        var receivedClip = new TaskCompletionSource<ClipboardPayload>();
        b.Host.ClipReceived += (_, clip) => receivedClip.TrySetResult(clip);

        byte[] fakePng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03 };
        var imageClip = new ClipboardPayload
        {
            Id = "img-" + Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ContentType = ClipboardContentType.Image,
            TextContent = "[Image]",
            SourceDevice = a.Settings.Current.DeviceId,
            MediaHash = "hash-12345",
            Width = 100,
            Height = 80,
            MediaBytes = (long)fakePng.Length
        };

        var peerB = a.Host.Peers.Single(p => p.DeviceId == b.Settings.Current.DeviceId);
        await a.Host.SendClipPayloadAsync(peerB, imageClip);

        var received = await receivedClip.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ClipboardContentType.Image, received.ContentType);
        Assert.Equal("hash-12345", received.MediaHash);
        Assert.Equal(100, received.Width);
        Assert.Equal(80, received.Height);
        Assert.Equal(fakePng.Length, received.MediaBytes);
    }

    [Fact]
    public async Task LockAll_SendsLockRequest_AndFiresOnPeer()
    {
        var (a, b) = await PairedAsync();
        await using var _a = a;
        await using var _b = b;

        var lockFired = new TaskCompletionSource<string>();
        b.Host.LockRequestedByPeer += senderId => lockFired.TrySetResult(senderId);

        await a.Host.LockPeersAsync();

        string sender = await lockFired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(a.Settings.Current.DeviceId, sender);
    }
}
