using System;

namespace Mouser.Core.Geometry;

/// <summary>
/// Normalizes and transforms cursor coordinates across heterogeneous displays with differing resolutions,
/// aspect ratios, and DPI scaling factors.
/// </summary>
public static class CoordinateTransformationEngine
{
    /// <summary>
    /// Calculates the entry point (TargetX, TargetY) on the target screen when exiting from source screen.
    /// </summary>
    public static (double TargetX, double TargetY) CalculateEntryPosition(
        double sourceExitX,
        double sourceExitY,
        ScreenBounds source,
        ScreenBounds target,
        ScreenEdge exitEdge,
        bool proportional = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (source.Width <= 0 || source.Height <= 0 || target.Width <= 0 || target.Height <= 0)
        {
            return (target.X, target.Y);
        }

        switch (exitEdge)
        {
            case ScreenEdge.Right:
            {
                // Exiting right edge of source -> enters left edge of target
                double entryX = target.X;
                double entryY = proportional
                    ? target.Y + (Math.Clamp((sourceExitY - source.Y) / source.Height, 0.0, 1.0) * target.Height)
                    : Math.Clamp(sourceExitY - source.Y + target.Y, target.Y, target.Bottom - 1);
                return (Math.Round(entryX), Math.Round(entryY));
            }

            case ScreenEdge.Left:
            {
                // Exiting left edge of source -> enters right edge of target
                double entryX = target.Right - 1;
                double entryY = proportional
                    ? target.Y + (Math.Clamp((sourceExitY - source.Y) / source.Height, 0.0, 1.0) * target.Height)
                    : Math.Clamp(sourceExitY - source.Y + target.Y, target.Y, target.Bottom - 1);
                return (Math.Round(entryX), Math.Round(entryY));
            }

            case ScreenEdge.Bottom:
            {
                // Exiting bottom edge of source -> enters top edge of target
                double entryX = proportional
                    ? target.X + (Math.Clamp((sourceExitX - source.X) / source.Width, 0.0, 1.0) * target.Width)
                    : Math.Clamp(sourceExitX - source.X + target.X, target.X, target.Right - 1);
                double entryY = target.Y;
                return (Math.Round(entryX), Math.Round(entryY));
            }

            case ScreenEdge.Top:
            {
                // Exiting top edge of source -> enters bottom edge of target
                double entryX = proportional
                    ? target.X + (Math.Clamp((sourceExitX - source.X) / source.Width, 0.0, 1.0) * target.Width)
                    : Math.Clamp(sourceExitX - source.X + target.X, target.X, target.Right - 1);
                double entryY = target.Bottom - 1;
                return (Math.Round(entryX), Math.Round(entryY));
            }

            default:
                return (target.X, target.Y);
        }
    }

    /// <summary>
    /// Normalizes movement deltas (dx, dy) when crossing screens with differing DPI scales.
    /// </summary>
    public static (short AdjustedDx, short AdjustedDy) NormalizeDelta(
        short dx,
        short dy,
        double sourceDpiScale,
        double targetDpiScale)
    {
        if (sourceDpiScale <= 0 || targetDpiScale <= 0)
        {
            return (dx, dy);
        }

        double ratio = targetDpiScale / sourceDpiScale;
        short adjDx = (short)Math.Round(dx * ratio);
        short adjDy = (short)Math.Round(dy * ratio);

        return (adjDx, adjDy);
    }
}
