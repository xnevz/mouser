using System;
using Mouser.Core.Geometry;
using Mouser.Core.Kvm;

namespace Mouser.Tests;

public class VirtualPlaneTests
{
    [Fact]
    public void OverlappingRectangles_RejectedOnInsert()
    {
        var plane = new VirtualPlane();
        plane.Add("local", new ScreenBounds { X = 0, Y = 0, Width = 1000, Height = 1000 });

        // Overlapping rectangle
        var overlapping = new ScreenBounds { X = 500, Y = 500, Width = 1000, Height = 1000 };

        Assert.Throws<InvalidOperationException>(() => plane.Add("peerB", overlapping));
    }

    [Fact]
    public void NonOverlapping_AdjacentScreens_AddSuccessfully()
    {
        var plane = new VirtualPlane();
        plane.Add("local", new ScreenBounds { X = 0, Y = 0, Width = 1000, Height = 1000 });
        plane.Add("peerB", new ScreenBounds { X = 1000, Y = 0, Width = 1000, Height = 1000 });
        plane.Add("peerC", new ScreenBounds { X = 2000, Y = 0, Width = 1000, Height = 1000 });

        Assert.Equal(3, plane.Screens.Count);
    }

    [Fact]
    public void ThreeInARow_MovingRightThroughBIntoC_AndBack()
    {
        // 3 screens in a horizontal row: Local [0..1000], B [1000..2000], C [2000..3000]
        var plane = new VirtualPlane();
        plane.Add("local", new ScreenBounds { X = 0, Y = 0, Width = 1000, Height = 1000 });
        plane.Add("peerB", new ScreenBounds { X = 1000, Y = 0, Width = 1000, Height = 1000 });
        plane.Add("peerC", new ScreenBounds { X = 2000, Y = 0, Width = 1000, Height = 1000 });

        // 1. Move inside Local: from (500, 500) by (+100, 0) -> (600, 500)
        var step1 = plane.Move("local", 500, 500, 100, 0);
        Assert.Equal("local", step1.ScreenId);
        Assert.Equal(600, step1.X);
        Assert.Equal(500, step1.Y);

        // 2. Move from Local edge (990, 500) by (+20, 0) -> cross into peerB at local x=10
        var step2 = plane.Move("local", 990, 500, 20, 0);
        Assert.Equal("peerB", step2.ScreenId);
        Assert.Equal(10, step2.X);
        Assert.Equal(500, step2.Y);

        // 3. Move across peerB: from (990, 500) by (+20, 0) -> cross into peerC at local x=10
        var step3 = plane.Move("peerB", 990, 500, 20, 0);
        Assert.Equal("peerC", step3.ScreenId);
        Assert.Equal(10, step3.X);
        Assert.Equal(500, step3.Y);

        // 4. Move inside peerC: from (10, 500) by (+100, 0) -> (110, 500)
        var step4 = plane.Move("peerC", 10, 500, 100, 0);
        Assert.Equal("peerC", step4.ScreenId);
        Assert.Equal(110, step4.X);
        Assert.Equal(500, step4.Y);

        // 5. Move backwards from peerC: from (10, 500) by (-20, 0) -> cross back into peerB at local x=990
        var step5 = plane.Move("peerC", 10, 500, -20, 0);
        Assert.Equal("peerB", step5.ScreenId);
        Assert.Equal(990, step5.X);
        Assert.Equal(500, step5.Y);

        // 6. Move backwards from peerB: from (10, 500) by (-20, 0) -> cross back into Local at local x=990
        var step6 = plane.Move("peerB", 10, 500, -20, 0);
        Assert.Equal("local", step6.ScreenId);
        Assert.Equal(990, step6.X);
        Assert.Equal(500, step6.Y);
    }

    [Fact]
    public void GapsBetweenScreens_StopsCursorAtBoundary()
    {
        var plane = new VirtualPlane();
        // Screen A: [0, 0, 1000, 1000]
        plane.Add("screenA", new ScreenBounds { X = 0, Y = 0, Width = 1000, Height = 1000 });
        // Screen B: [1200, 0, 1000, 1000] (200 px gap from 1000 to 1200)
        plane.Add("screenB", new ScreenBounds { X = 1200, Y = 0, Width = 1000, Height = 1000 });

        // Cursor at (990, 500) on Screen A moves +50 px right into the gap (x=1040)
        var result = plane.Move("screenA", 990, 500, 50, 0);

        // Must stay on Screen A and stop at the boundary (Right - 1 = 999)
        Assert.Equal("screenA", result.ScreenId);
        Assert.Equal(999, result.X);
        Assert.Equal(500, result.Y);
    }
}
