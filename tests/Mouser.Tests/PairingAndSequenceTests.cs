using System.Net;
using Mouser.Core.Networking;

namespace Mouser.Tests;

public class PairingAndSequenceTests
{
    private static readonly IPAddress Phone = IPAddress.Parse("10.0.0.7");

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
