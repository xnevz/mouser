using System.Net;
using Mouser.Core.Networking;
using Mouser.Core.Services;

namespace Mouser.Tests;

public class UdpNetworkingTests
{
    [Fact]
    public async Task Receiver_ReceivesPacketsFromPeerLink()
    {
        using var receiver = new UdpInputReceiver(0);
        receiver.Start();
        var received = new List<InputMotionPacket>();
        receiver.MotionReceived += (packet, _) =>
        {
            if (packet.PacketType == InputMotionPacket.TypeMotion) lock (received) received.Add(packet);
        };

        using var link = new PeerLink(new IPEndPoint(IPAddress.Loopback, receiver.BoundPort));
        for (short i = 1; i <= 5; i++) link.Send(InputMotionPacket.TypeMotion, (short)(i * 10), (short)(i * -5));

        await Wait.UntilAsync(() => { lock (received) return received.Count >= 5; });
        lock (received)
        {
            Assert.Equal(10, received[0].DeltaX);
            Assert.Equal(-5, received[0].DeltaY);
        }
    }

    [Fact]
    public void PeerLink_Sending1000Packets_AllocatesNothing()
    {
        using var receiver = new UdpInputReceiver(0);
        using var link = new PeerLink(new IPEndPoint(IPAddress.Loopback, receiver.BoundPort));

        for (int i = 0; i < 10; i++) link.Send(InputMotionPacket.TypeMotion, 1, 1); // JIT warm-up
        ushort before = link.CurrentSequence;

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (short i = 0; i < 1000; i++) link.Send(InputMotionPacket.TypeMoveAbsolute, i, (short)-i, 0x01);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(0, allocated);
        // The heartbeat timer may slip a ping or two into the sequence while we send.
        Assert.InRange((ushort)(link.CurrentSequence - before), 1000, 1005);
    }

    [Fact]
    public async Task PeerLink_PairsAndTracksLiveness()
    {
        var now = DateTime.UtcNow;
        var gate = new PairingGate(424_242);
        using var host = new InputInjectionService(new RecordingSink(), gate, 0);
        host.Start();
        using var link = new PeerLink(new IPEndPoint(IPAddress.Loopback, host.BoundPort), () => now);

        Assert.False(await link.PairAsync(111_111));
        Assert.False(link.IsAlive);

        Assert.True(await link.PairAsync(424_242));
        Assert.True(link.IsPaired);
        Assert.True(link.IsAlive);

        // Host stops answering: the link must report it dead so the KVM hands control back.
        host.Stop();
        now += PeerLink.PongTimeout + TimeSpan.FromSeconds(1);
        Assert.False(link.IsAlive);
    }

    [Fact]
    public async Task PeerLink_ReportsLostPairingWhenHostChangesPin()
    {
        var gate = new PairingGate(424_242);
        using var host = new InputInjectionService(new RecordingSink(), gate, 0);
        host.Start();
        using var link = new PeerLink(new IPEndPoint(IPAddress.Loopback, host.BoundPort));
        var lost = new TaskCompletionSource();
        link.PairingLost += () => lost.TrySetResult();

        Assert.True(await link.PairAsync(424_242));
        gate.Pin = 999_999;

        Assert.Same(lost.Task, await Task.WhenAny(lost.Task, Task.Delay(3000)));
        Assert.False(link.IsPaired);
    }
}
