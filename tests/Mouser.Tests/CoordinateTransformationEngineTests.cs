using System;
using Mouser.Core.Geometry;
using Xunit;

namespace Mouser.Tests;

public sealed class CoordinateTransformationEngineTests
{
    [Fact]
    public void CalculateEntryPosition_WhenProportionalFalse_PreservesExactPixelPosition()
    {
        var sourceScreen = new ScreenBounds
        {
            X = 0,
            Y = 0,
            Width = 1920,
            Height = 1080
        };

        var targetScreen = new ScreenBounds
        {
            X = 1920,
            Y = 0,
            Width = 2560,
            Height = 1440
        };

        // When proportional = true, exit y=540 (halfway down 1080) maps to y=720 (halfway down 1440)
        var (propX, propY) = CoordinateTransformationEngine.CalculateEntryPosition(
            sourceExitX: 1920,
            sourceExitY: 540,
            source: sourceScreen,
            target: targetScreen,
            exitEdge: ScreenEdge.Right,
            proportional: true);

        Assert.Equal(1920, propX);
        Assert.Equal(720, propY);

        // When proportional = false, exit y=540 maps to exact pixel y=540
        var (pixelX, pixelY) = CoordinateTransformationEngine.CalculateEntryPosition(
            sourceExitX: 1920,
            sourceExitY: 540,
            source: sourceScreen,
            target: targetScreen,
            exitEdge: ScreenEdge.Right,
            proportional: false);

        Assert.Equal(1920, pixelX);
        Assert.Equal(540, pixelY);
    }

    [Fact]
    public void CalculateEntryPosition_WhenProportionalFalse_ClampsToTargetBounds()
    {
        var sourceScreen = new ScreenBounds
        {
            X = 0,
            Y = 0,
            Width = 2560,
            Height = 1440
        };

        var targetScreen = new ScreenBounds
        {
            X = 2560,
            Y = 0,
            Width = 1920,
            Height = 1080
        };

        // Exit at y=1200 on source (1440 tall) when entering target (1080 tall)
        // With proportional = false, 1200 exceeds target.Bottom - 1 (1079), so it clamps to 1079
        var (pixelX, pixelY) = CoordinateTransformationEngine.CalculateEntryPosition(
            sourceExitX: 2560,
            sourceExitY: 1200,
            source: sourceScreen,
            target: targetScreen,
            exitEdge: ScreenEdge.Right,
            proportional: false);

        Assert.Equal(2560, pixelX);
        Assert.Equal(1079, pixelY);
    }
}
