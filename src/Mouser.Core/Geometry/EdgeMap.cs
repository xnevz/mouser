using System;
using System.Collections.Generic;

namespace Mouser.Core.Geometry;

public sealed record Monitor(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool Contains(double px, double py) =>
        px >= X && px <= Right && py >= Y && py <= Bottom;
}

public static class EdgeMap
{
    /// <summary>
    /// Local monitors and a peer's placed monitors share one coordinate plane. Given a cursor at (x,y) moving
    /// toward <paramref name="edge"/>, returns the peer monitor that is directly across that edge at that
    /// position (overlapping along the edge), or null when the cursor would leave into empty space.
    /// </summary>
    public static (int PeerMonitorIndex, double EntryX, double EntryY)? FindCrossing(
        IReadOnlyList<Monitor> local, IReadOnlyList<Monitor> peerPlaced, double x, double y, ScreenEdge edge)
    {
        if (local == null || peerPlaced == null || local.Count == 0 || peerPlaced.Count == 0 || edge == ScreenEdge.None)
            return null;

        if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
            return null;

        if (!IsOuterEdge(local, x, y, edge))
            return null;

        for (int m = 0; m < local.Count; m++)
        {
            var localMon = local[m];
            if (!IsNearEdge(localMon, x, y, edge))
                continue;

            if (IsSharedWithOtherLocal(local, m, x, y, edge))
                continue;

            for (int p = 0; p < peerPlaced.Count; p++)
            {
                var peer = peerPlaced[p];
                if (TryMatchPeer(localMon, peer, x, y, edge, out double entryX, out double entryY))
                {
                    return (p, entryX, entryY);
                }
            }
        }

        return null;
    }

    /// <summary>True when (x,y) lies on an outer edge of some local monitor (not an edge shared with another local monitor).</summary>
    public static bool IsOuterEdge(IReadOnlyList<Monitor> local, double x, double y, ScreenEdge edge)
    {
        if (local == null || local.Count == 0 || edge == ScreenEdge.None)
            return false;

        if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
            return false;

        for (int i = 0; i < local.Count; i++)
        {
            var localMon = local[i];
            if (!IsNearEdge(localMon, x, y, edge))
                continue;

            if (!IsSharedWithOtherLocal(local, i, x, y, edge))
                return true;
        }

        return false;
    }

    private static bool IsNearEdge(Monitor m, double x, double y, ScreenEdge edge)
    {
        const double margin = 3.0;

        if (x < m.X - margin || x > m.Right + margin ||
            y < m.Y - margin || y > m.Bottom + margin)
        {
            return false;
        }

        return edge switch
        {
            ScreenEdge.Right => Math.Abs(x - m.Right) <= margin,
            ScreenEdge.Left => Math.Abs(x - m.X) <= margin,
            ScreenEdge.Bottom => Math.Abs(y - m.Bottom) <= margin,
            ScreenEdge.Top => Math.Abs(y - m.Y) <= margin,
            _ => false
        };
    }

    private static bool IsSharedWithOtherLocal(
        IReadOnlyList<Monitor> local, int localMonIndex, double x, double y, ScreenEdge edge)
    {
        const double tolerance = 2.0;
        var localMon = local[localMonIndex];

        for (int i = 0; i < local.Count; i++)
        {
            if (i == localMonIndex)
                continue;

            var other = local[i];
            switch (edge)
            {
                case ScreenEdge.Right:
                    if (Math.Abs(other.X - localMon.Right) <= tolerance &&
                        y >= other.Y - tolerance && y <= other.Bottom + tolerance)
                    {
                        return true;
                    }
                    break;

                case ScreenEdge.Left:
                    if (Math.Abs(other.Right - localMon.X) <= tolerance &&
                        y >= other.Y - tolerance && y <= other.Bottom + tolerance)
                    {
                        return true;
                    }
                    break;

                case ScreenEdge.Bottom:
                    if (Math.Abs(other.Y - localMon.Bottom) <= tolerance &&
                        x >= other.X - tolerance && x <= other.Right + tolerance)
                    {
                        return true;
                    }
                    break;

                case ScreenEdge.Top:
                    if (Math.Abs(other.Bottom - localMon.Y) <= tolerance &&
                        x >= other.X - tolerance && x <= other.Right + tolerance)
                    {
                        return true;
                    }
                    break;
            }
        }

        return false;
    }

    private static bool TryMatchPeer(
        Monitor localMon, Monitor peer, double x, double y, ScreenEdge edge,
        out double entryX, out double entryY)
    {
        const double tolerance = 2.0;
        entryX = 0;
        entryY = 0;

        switch (edge)
        {
            case ScreenEdge.Right:
                if (Math.Abs(peer.X - localMon.Right) <= tolerance &&
                    y >= peer.Y - tolerance && y <= peer.Bottom + tolerance)
                {
                    entryX = peer.X;
                    entryY = Math.Clamp(y, peer.Y, Math.Max(peer.Y, peer.Bottom - 1));
                    return true;
                }
                break;

            case ScreenEdge.Left:
                if (Math.Abs(peer.Right - localMon.X) <= tolerance &&
                    y >= peer.Y - tolerance && y <= peer.Bottom + tolerance)
                {
                    entryX = Math.Max(peer.X, peer.Right - 1);
                    entryY = Math.Clamp(y, peer.Y, Math.Max(peer.Y, peer.Bottom - 1));
                    return true;
                }
                break;

            case ScreenEdge.Bottom:
                if (Math.Abs(peer.Y - localMon.Bottom) <= tolerance &&
                    x >= peer.X - tolerance && x <= peer.Right + tolerance)
                {
                    entryX = Math.Clamp(x, peer.X, Math.Max(peer.X, peer.Right - 1));
                    entryY = peer.Y;
                    return true;
                }
                break;

            case ScreenEdge.Top:
                if (Math.Abs(peer.Bottom - localMon.Y) <= tolerance &&
                    x >= peer.X - tolerance && x <= peer.Right + tolerance)
                {
                    entryX = Math.Clamp(x, peer.X, Math.Max(peer.X, peer.Right - 1));
                    entryY = Math.Max(peer.Y, peer.Bottom - 1);
                    return true;
                }
                break;
        }

        return false;
    }
}
