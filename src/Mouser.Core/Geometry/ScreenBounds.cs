using System;

namespace Mouser.Core.Geometry;

/// <summary>
/// Defines the physical or virtual bounds and DPI resolution parameters of a display.
/// </summary>
public sealed record ScreenBounds
{
    public string HostId { get; init; } = string.Empty;
    public string Hostname { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double DpiScale { get; init; } = 1.0;
    public bool IsLocal { get; init; }

    public double Right => X + Width;
    public double Bottom => Y + Height;

    public bool Contains(double px, double py)
    {
        return px >= X && px <= Right && py >= Y && py <= Bottom;
    }

    public double DistanceToEdge(double px, double py, ScreenEdge edge) => edge switch
    {
        ScreenEdge.Left => Math.Abs(px - X),
        ScreenEdge.Right => Math.Abs(px - Right),
        ScreenEdge.Top => Math.Abs(py - Y),
        ScreenEdge.Bottom => Math.Abs(py - Bottom),
        _ => double.MaxValue
    };
}
