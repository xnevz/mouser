using Mouser.Core.Kvm;

namespace Mouser.Tests;

public class MoveCoalescerTests
{
    [Fact]
    public void InitialPost_SendsImmediately()
    {
        var sent = new List<(short X, short Y)>();
        long fakeClock = 0;
        var coalescer = new MoveCoalescer((x, y) => sent.Add((x, y)), minIntervalMs: 2, clock: () => fakeClock);

        coalescer.Post(10, 20);

        Assert.Single(sent);
        Assert.Equal(((short)10, (short)20), sent[0]);
    }

    [Fact]
    public void Coalesce1000Posts_Within100Ms_ResultsInAtMost60Sends()
    {
        var sent = new List<(short X, short Y)>();
        long fakeClock = 0;
        var coalescer = new MoveCoalescer((x, y) => sent.Add((x, y)), minIntervalMs: 2, clock: () => fakeClock);

        // 1000 posts within 100 ms (10 posts per ms)
        for (int i = 0; i < 1000; i++)
        {
            fakeClock = i / 10; // 0 to 99 ms
            coalescer.Post((short)i, (short)i);
        }
        coalescer.Flush();

        Assert.InRange(sent.Count, 1, 60);
        Assert.Equal(((short)999, (short)999), sent[^1]);
    }

    [Fact]
    public void LastPosition_AlwaysSentOnFlush()
    {
        var sent = new List<(short X, short Y)>();
        long fakeClock = 1000;
        var coalescer = new MoveCoalescer((x, y) => sent.Add((x, y)), minIntervalMs: 2, clock: () => fakeClock);

        coalescer.Post(10, 20); // sent immediately at 1000
        Assert.Single(sent);

        fakeClock = 1001; // < 2 ms elapsed
        coalescer.Post(30, 40); // pending
        Assert.Single(sent); // still only the first post sent

        coalescer.Flush();
        Assert.Equal(2, sent.Count);
        Assert.Equal(((short)30, (short)40), sent[1]);

        // Calling Flush again when no pending moves does not send anything duplicate
        coalescer.Flush();
        Assert.Equal(2, sent.Count);
    }

    [Fact]
    public void FlushBeforeButtonKeyScroll_PreservesOrder()
    {
        var eventLog = new List<string>();
        long fakeClock = 0;
        var coalescer = new MoveCoalescer((x, y) => eventLog.Add($"move {x},{y}"), minIntervalMs: 2, clock: () => fakeClock);

        coalescer.Post(100, 200); // sent immediately

        fakeClock = 1; // within 2 ms
        coalescer.Post(105, 205); // pending

        // Simulate incoming Button / Key / Wheel event in KvmController:
        coalescer.Flush();
        eventLog.Add("button down");

        Assert.Equal(new[]
        {
            "move 100,200",
            "move 105,205",
            "button down"
        }, eventLog);
    }
}
