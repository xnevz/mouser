using System.Net;
using Mouser.Core.Networking;

namespace Mouser.Tests;

public class PairingAndSequenceTests
{
    private static readonly IPAddress Phone = IPAddress.Parse("10.0.0.7");

    [Fact]
    public void Pin_RoundTripsThroughPacket()
    {
        foreach (int pin in new[] { 0, 7, 65_535, 65_536, 482_913, 999_999 })
        {
            var packet = InputMotionPacket.CreatePair(1, pin);
            var buffer = new byte[InputMotionPacket.PacketSize];
            packet.TryWrite(buffer);
            Assert.True(InputMotionPacket.TryRead(buffer, out var parsed));
            Assert.Equal(pin, parsed.PinValue);
        }
    }

    [Fact]
    public void PairPacket_BytesMatchTheAndroidClient()
    {
        // Same expectation as android ProtocolTest.pairPacket_bytesMatchTheDesktopLayout.
        var buffer = new byte[InputMotionPacket.PacketSize];
        InputMotionPacket.CreatePair(0x0102, 482_913).TryWrite(buffer);
        Assert.Equal(new byte[] { 0x4D, 0x4F, 0x55, 0x53, 0x01, 0x06, 0x02, 0x01, 0x07, 0x00, 0x61, 0x5E, 0x00, 0x00 }, buffer);
    }

    [Fact]
    public void GeneratedPins_AreSixDigits()
    {
        for (int i = 0; i < 200; i++)
        {
            int pin = PairingGate.GeneratePin();
            Assert.InRange(pin, 0, 999_999);
            Assert.Matches(@"^\d{3} \d{3}$", PairingGate.FormatPin(pin));
        }
    }

    [Fact]
    public void RepeatedWrongPins_LockTheSenderOut()
    {
        var now = DateTime.UtcNow;
        var gate = new PairingGate(111_111, () => now);

        for (int i = 0; i < PairingGate.MaxFailedAttempts; i++) Assert.False(gate.TryPair(Phone, 222_222));

        // Even the right PIN is refused while locked out.
        Assert.False(gate.TryPair(Phone, 111_111));

        now += PairingGate.LockoutDuration + TimeSpan.FromSeconds(1);
        Assert.True(gate.TryPair(Phone, 111_111));
    }

    [Fact]
    public void ChangingPin_UnpairsEveryone()
    {
        var gate = new PairingGate(111_111);
        Assert.True(gate.TryPair(Phone, 111_111));

        gate.Pin = 333_333;

        Assert.False(gate.IsTrusted(Phone));
    }

    [Fact]
    public void Ipv4MappedAddresses_AreTreatedAsTheSameDevice()
    {
        var gate = new PairingGate(111_111);
        gate.TryPair(Phone.MapToIPv6(), 111_111);
        Assert.True(gate.IsTrusted(Phone));
    }

    [Fact]
    public void Sequence_AcceptsIncreasing_RejectsDuplicatesAndLatePackets()
    {
        var tracker = new SequenceTracker();
        var ep = new IPEndPoint(Phone, 1);

        Assert.True(tracker.Accept(ep, 100));
        Assert.True(tracker.Accept(ep, 101));
        Assert.False(tracker.Accept(ep, 101));
        Assert.False(tracker.Accept(ep, 99));
        Assert.True(tracker.Accept(ep, 150));
    }

    [Fact]
    public void Sequence_HandlesSixteenBitWrapAround()
    {
        var tracker = new SequenceTracker();
        var ep = new IPEndPoint(Phone, 1);

        Assert.True(tracker.Accept(ep, 65_534));
        Assert.True(tracker.Accept(ep, 65_535));
        Assert.True(tracker.Accept(ep, 0));
        Assert.True(tracker.Accept(ep, 1));
        Assert.False(tracker.Accept(ep, 65_535));
    }

    [Fact]
    public void Sequence_TreatsBigBackwardsJumpAsSenderRestart()
    {
        var tracker = new SequenceTracker();
        var ep = new IPEndPoint(Phone, 1);

        Assert.True(tracker.Accept(ep, 20_000));
        Assert.True(tracker.Accept(ep, 1)); // app restarted and counts from 1 again
        Assert.True(tracker.Accept(ep, 2));
    }

    [Fact]
    public void Sequence_IsTrackedPerSender()
    {
        var tracker = new SequenceTracker();
        Assert.True(tracker.Accept(new IPEndPoint(Phone, 1), 500));
        Assert.True(tracker.Accept(new IPEndPoint(Phone, 2), 3));
    }
}
