using Mouser.Core.Clipboard;
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
        // Each side learns the other from its once-a-second beacon; wait until both have.
        await Wait.UntilAsync(() => a.Host.Peers.Any(p => p.DeviceId == b.Settings.Current.DeviceId)
                                    && b.Host.Peers.Any(p => p.DeviceId == a.Settings.Current.DeviceId), timeoutMs: 5000);

        var peerB = a.Host.Peers.Single(p => p.DeviceId == b.Settings.Current.DeviceId);
        Assert.False(await a.Host.PairPeerAsync(peerB, 111_111)); // wrong PIN
        Assert.True(await a.Host.PairPeerAsync(peerB, b.Settings.Current.PairingPin));
        return (a, b);
    }

    [Fact]
    public async Task PairingSavesThePeer_PlacesItRight_AndReportsItPaired()
    {
        var (a, b) = await PairedAsync();
        await using var _a = a;
        await using var _b = b;

        var saved = a.Settings.Current.Peers[b.Settings.Current.DeviceId];
        Assert.Equal(b.Settings.Current.PairingPin, saved.Pin);
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
}
