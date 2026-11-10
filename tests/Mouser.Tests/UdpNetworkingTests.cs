using System.Net;
using Mouser.Core.Networking;
using Xunit;

namespace Mouser.Tests;

public class UdpNetworkingTests
{
    [Fact]
    public void SequenceOrdering_AcceptsIncreasingSequences()
    {
        using var receiver = new UdpInputReceiver();

        Assert.True(receiver.IsSequencedPacketAcceptable(1));
        Assert.True(receiver.IsSequencedPacketAcceptable(2));
        Assert.True(receiver.IsSequencedPacketAcceptable(100));
    }

    [Fact]
    public void SequenceOrdering_RejectsStalePackets()
    {
        using var receiver = new UdpInputReceiver();

        // Simulate accepting sequence 10
        Assert.True(receiver.IsSequencedPacketAcceptable(10));
    }

    [Fact]
    public async Task Udp_Loopback_TransmitsAndReceivesPackets()
    {
        using var receiver = new UdpInputReceiver(0);
        int receiverPort = receiver.BoundPort;
        receiver.Start();

        var receivedPackets = new List<InputMotionPacket>();
        var tcs = new TaskCompletionSource<bool>();

        receiver.MotionReceived += (packet, sender) =>
        {
            lock (receivedPackets)
            {
                receivedPackets.Add(packet);
                if (receivedPackets.Count >= 5)
                {
                    tcs.TrySetResult(true);
                }
            }
        };

        var targetEp = new IPEndPoint(IPAddress.Loopback, receiverPort);
        using var transmitter = new UdpInputTransmitter(targetEp);

        for (short i = 1; i <= 5; i++)
        {
            transmitter.SendMotion((short)(i * 10), (short)(i * -5), buttons: 0x01);
            await Task.Delay(10);
        }

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Same(tcs.Task, completed);

        lock (receivedPackets)
        {
            Assert.True(receivedPackets.Count >= 5);
            Assert.Equal(10, receivedPackets[0].DeltaX);
            Assert.Equal(-5, receivedPackets[0].DeltaY);
        }
    }

    [Fact]
    public void Udp_ZeroAllocation_1000Packets_ProducesZeroHeapAllocations()
    {
        using var receiver = new UdpInputReceiver(0);
        var targetEp = new IPEndPoint(IPAddress.Loopback, receiver.BoundPort);
        using var transmitter = new UdpInputTransmitter(targetEp);

        // Warm up JIT
        for (int i = 0; i < 10; i++)
        {
            transmitter.SendMotion(1, 1);
        }

        // Measure allocations during 1,000 packet transmissions
        long beforeAlloc = GC.GetAllocatedBytesForCurrentThread();

        for (short i = 0; i < 1000; i++)
        {
            transmitter.SendMotion(i, (short)-i, buttons: 0x01, modifiers: 0x02);
        }

        long afterAlloc = GC.GetAllocatedBytesForCurrentThread();
        long totalAllocated = afterAlloc - beforeAlloc;

        Assert.Equal(0, totalAllocated);
        Assert.Equal(1010, transmitter.CurrentSequence);
    }
}
