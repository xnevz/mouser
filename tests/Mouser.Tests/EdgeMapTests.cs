using System.Collections.Generic;
using Mouser.Core.Geometry;
using Xunit;
using Monitor = Mouser.Core.Geometry.Monitor;

namespace Mouser.Tests;

public class EdgeMapTests
{
    private static readonly Monitor Local1080p = new(0, 0, 1920, 1080);
    private static readonly Monitor Peer1080pRight = new(1920, 0, 1920, 1080);

    [Fact]
    public void Monitor_Record_CalculatesRightBottomAndContains()
    {
        var mon = new Monitor(100, 200, 1920, 1080);
        Assert.Equal(2020, mon.Right);
        Assert.Equal(1280, mon.Bottom);
        Assert.True(mon.Contains(100, 200));
        Assert.True(mon.Contains(2020, 1280));
        Assert.True(mon.Contains(500, 500));
        Assert.False(mon.Contains(99, 500));
        Assert.False(mon.Contains(2021, 500));
        Assert.False(mon.Contains(500, 199));
        Assert.False(mon.Contains(500, 1281));
    }

    [Fact]
    public void Test1_SideBySide_RightEdgeCrossing_CrossesWithPixelAlignedY()
    {
        var local = new[] { Local1080p };
        var peer = new[] { Peer1080pRight };

        var crossing = EdgeMap.FindCrossing(local, peer, 1920, 500, ScreenEdge.Right);

        Assert.NotNull(crossing);
        Assert.Equal(0, crossing.Value.PeerMonitorIndex);
        Assert.Equal(1920, crossing.Value.EntryX);
        Assert.Equal(500, crossing.Value.EntryY);
    }

    [Fact]
    public void Test2_PeerTallerThanLocal_TopAndBottomHalfCross_PixelAlignedY()
    {
        var local = new[] { new Monitor(0, 0, 1920, 1080) };
        var peerTaller = new[] { new Monitor(1920, 0, 1920, 1440) };

        // Cursor in top half
        var topCrossing = EdgeMap.FindCrossing(local, peerTaller, 1920, 200, ScreenEdge.Right);
        Assert.NotNull(topCrossing);
        Assert.Equal(0, topCrossing.Value.PeerMonitorIndex);
        Assert.Equal(1920, topCrossing.Value.EntryX);
        Assert.Equal(200, topCrossing.Value.EntryY);

        // Cursor in bottom half
        var bottomCrossing = EdgeMap.FindCrossing(local, peerTaller, 1920, 900, ScreenEdge.Right);
        Assert.NotNull(bottomCrossing);
        Assert.Equal(0, bottomCrossing.Value.PeerMonitorIndex);
        Assert.Equal(1920, bottomCrossing.Value.EntryX);
        Assert.Equal(900, bottomCrossing.Value.EntryY);
    }

    [Fact]
    public void Test3_PeerShorterThanLocal_CursorBelowPeerBottom_ReturnsNull()
    {
        var local = new[] { new Monitor(0, 0, 1920, 1440) };
        var peerShorter = new[] { new Monitor(1920, 0, 1920, 1080) };

        // y = 1200 is below peer's bottom (1080)
        var crossing = EdgeMap.FindCrossing(local, peerShorter, 1920, 1200, ScreenEdge.Right);
        Assert.Null(crossing);

        // y = 500 is within peer's vertical range -> crosses successfully
        var validCrossing = EdgeMap.FindCrossing(local, peerShorter, 1920, 500, ScreenEdge.Right);
        Assert.NotNull(validCrossing);
        Assert.Equal(0, validCrossing.Value.PeerMonitorIndex);
        Assert.Equal(1920, validCrossing.Value.EntryX);
        Assert.Equal(500, validCrossing.Value.EntryY);
    }

    [Fact]
    public void Test4_TwoLocalMonitorsStacked_PeerOnlyBesideTop_CrossTopSucceeds_CrossBottomNull()
    {
        var topLocal = new Monitor(0, 0, 1920, 1080);
        var bottomLocal = new Monitor(0, 1080, 1920, 1080);
        var local = new[] { topLocal, bottomLocal };
        var peer = new[] { new Monitor(1920, 0, 1920, 1080) }; // only beside top

        // Crossing right from top monitor (y = 500)
        var topCrossing = EdgeMap.FindCrossing(local, peer, 1920, 500, ScreenEdge.Right);
        Assert.NotNull(topCrossing);
        Assert.Equal(0, topCrossing.Value.PeerMonitorIndex);
        Assert.Equal(1920, topCrossing.Value.EntryX);
        Assert.Equal(500, topCrossing.Value.EntryY);

        // Crossing right from bottom monitor (y = 1500) -> peer does not overlap
        var bottomCrossing = EdgeMap.FindCrossing(local, peer, 1920, 1500, ScreenEdge.Right);
        Assert.Null(bottomCrossing);
    }

    [Fact]
    public void Test5_InnerEdgeBetweenStackedMonitors_IsOuterEdgeIsFalse()
    {
        var topLocal = new Monitor(0, 0, 1920, 1080);
        var bottomLocal = new Monitor(0, 1080, 1920, 1080);
        var local = new[] { topLocal, bottomLocal };

        // Moving down across bottom edge of top monitor (y = 1080, edge = Bottom)
        Assert.False(EdgeMap.IsOuterEdge(local, 960, 1080, ScreenEdge.Bottom));

        // Moving up across top edge of bottom monitor (y = 1080, edge = Top)
        Assert.False(EdgeMap.IsOuterEdge(local, 960, 1080, ScreenEdge.Top));

        // FindCrossing at inner edge returns null even if peer is specified
        var peer = new[] { new Monitor(0, 1080, 1920, 1080) };
        Assert.Null(EdgeMap.FindCrossing(local, peer, 960, 1080, ScreenEdge.Bottom));
    }

    [Fact]
    public void Test6_OuterEdgeOfStackedMonitors_IsOuterEdgeIsTrue()
    {
        var topLocal = new Monitor(0, 0, 1920, 1080);
        var bottomLocal = new Monitor(0, 1080, 1920, 1080);
        var local = new[] { topLocal, bottomLocal };

        // Top edge of top monitor (y = 0, edge = Top)
        Assert.True(EdgeMap.IsOuterEdge(local, 960, 0, ScreenEdge.Top));

        // Bottom edge of bottom monitor (y = 2160, edge = Bottom)
        Assert.True(EdgeMap.IsOuterEdge(local, 960, 2160, ScreenEdge.Bottom));

        // Outer right edges of both monitors are outer edges
        Assert.True(EdgeMap.IsOuterEdge(local, 1920, 500, ScreenEdge.Right));
        Assert.True(EdgeMap.IsOuterEdge(local, 1920, 1500, ScreenEdge.Right));
    }

    [Fact]
    public void Test7_GapUpTo2Px_CountsAsAdjacent_CrossingSucceeds()
    {
        var local = new[] { new Monitor(0, 0, 1920, 1080) };

        // Gap of 1 px (peer placed at X = 1921)
        var peerGap1 = new[] { new Monitor(1921, 0, 1920, 1080) };
        var crossing1 = EdgeMap.FindCrossing(local, peerGap1, 1920, 500, ScreenEdge.Right);
        Assert.NotNull(crossing1);
        Assert.Equal(1921, crossing1.Value.EntryX);

        // Gap of 2 px (peer placed at X = 1922)
        var peerGap2 = new[] { new Monitor(1922, 0, 1920, 1080) };
        var crossing2 = EdgeMap.FindCrossing(local, peerGap2, 1920, 500, ScreenEdge.Right);
        Assert.NotNull(crossing2);
        Assert.Equal(1922, crossing2.Value.EntryX);
    }

    [Fact]
    public void Test8_GapOf10Px_DoesNotCountAsAdjacent_ReturnsNull()
    {
        var local = new[] { new Monitor(0, 0, 1920, 1080) };
        var peerGap10 = new[] { new Monitor(1930, 0, 1920, 1080) };

        var crossing = EdgeMap.FindCrossing(local, peerGap10, 1920, 500, ScreenEdge.Right);
        Assert.Null(crossing);
    }

    [Fact]
    public void Test9_LeftEdgeCrossing_SucceedsWithCorrectEntryCoordinates()
    {
        var local = new[] { new Monitor(1920, 0, 1920, 1080) };
        var peerLeft = new[] { new Monitor(0, 0, 1920, 1080) };

        var crossing = EdgeMap.FindCrossing(local, peerLeft, 1920, 450, ScreenEdge.Left);
        Assert.NotNull(crossing);
        Assert.Equal(0, crossing.Value.PeerMonitorIndex);
        Assert.Equal(1919, crossing.Value.EntryX); // peer.Right - 1
        Assert.Equal(450, crossing.Value.EntryY);
    }

    [Fact]
    public void Test10_BottomEdgeCrossing_SucceedsWithCorrectEntryCoordinates()
    {
        var local = new[] { new Monitor(0, 0, 1920, 1080) };
        var peerBottom = new[] { new Monitor(0, 1080, 1920, 1080) };

        var crossing = EdgeMap.FindCrossing(local, peerBottom, 750, 1080, ScreenEdge.Bottom);
        Assert.NotNull(crossing);
        Assert.Equal(0, crossing.Value.PeerMonitorIndex);
        Assert.Equal(750, crossing.Value.EntryX);
        Assert.Equal(1080, crossing.Value.EntryY); // peer.Y
    }

    [Fact]
    public void Test11_TopEdgeCrossing_SucceedsWithCorrectEntryCoordinates()
    {
        var local = new[] { new Monitor(0, 1080, 1920, 1080) };
        var peerTop = new[] { new Monitor(0, 0, 1920, 1080) };

        var crossing = EdgeMap.FindCrossing(local, peerTop, 750, 1080, ScreenEdge.Top);
        Assert.NotNull(crossing);
        Assert.Equal(0, crossing.Value.PeerMonitorIndex);
        Assert.Equal(750, crossing.Value.EntryX);
        Assert.Equal(1079, crossing.Value.EntryY); // peer.Bottom - 1
    }

    [Fact]
    public void MultiplePeerMonitors_SelectsCorrectPeerIndex()
    {
        var local = new[] { new Monitor(0, 0, 1920, 2160) }; // tall local
        var peers = new[]
        {
            new Monitor(1920, 0, 1920, 1080),    // index 0: top half
            new Monitor(1920, 1080, 1920, 1080)  // index 1: bottom half
        };

        var topCrossing = EdgeMap.FindCrossing(local, peers, 1920, 300, ScreenEdge.Right);
        Assert.NotNull(topCrossing);
        Assert.Equal(0, topCrossing.Value.PeerMonitorIndex);
        Assert.Equal(1920, topCrossing.Value.EntryX);
        Assert.Equal(300, topCrossing.Value.EntryY);

        var bottomCrossing = EdgeMap.FindCrossing(local, peers, 1920, 1600, ScreenEdge.Right);
        Assert.NotNull(bottomCrossing);
        Assert.Equal(1, bottomCrossing.Value.PeerMonitorIndex);
        Assert.Equal(1920, bottomCrossing.Value.EntryX);
        Assert.Equal(1600, bottomCrossing.Value.EntryY);
    }

    [Fact]
    public void InvalidInputs_OrNoneEdge_ReturnNullAndFalse()
    {
        var local = new[] { Local1080p };
        var peer = new[] { Peer1080pRight };

        Assert.False(EdgeMap.IsOuterEdge(local, 1920, 500, ScreenEdge.None));
        Assert.Null(EdgeMap.FindCrossing(local, peer, 1920, 500, ScreenEdge.None));

        Assert.False(EdgeMap.IsOuterEdge(null!, 1920, 500, ScreenEdge.Right));
        Assert.Null(EdgeMap.FindCrossing(null!, peer, 1920, 500, ScreenEdge.Right));
        Assert.Null(EdgeMap.FindCrossing(local, null!, 1920, 500, ScreenEdge.Right));

        Assert.False(EdgeMap.IsOuterEdge(new Monitor[0], 1920, 500, ScreenEdge.Right));
        Assert.Null(EdgeMap.FindCrossing(new Monitor[0], peer, 1920, 500, ScreenEdge.Right));
        Assert.Null(EdgeMap.FindCrossing(local, new Monitor[0], 1920, 500, ScreenEdge.Right));

        // Cursor in middle of screen is not on outer edge
        Assert.False(EdgeMap.IsOuterEdge(local, 960, 540, ScreenEdge.Right));
        Assert.Null(EdgeMap.FindCrossing(local, peer, 960, 540, ScreenEdge.Right));
    }
}
