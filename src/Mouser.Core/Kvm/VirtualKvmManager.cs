using System;
using System.Collections.Generic;
using System.Linq;
using Mouser.Core.Geometry;
using Monitor = Mouser.Core.Geometry.Monitor;

namespace Mouser.Core.Kvm;

public sealed class CursorTransitionEventArgs : EventArgs
{
    public string TargetHostId { get; }
    public string TargetHostname { get; }
    public double EntryX { get; }
    public double EntryY { get; }
    public ScreenEdge TransitionEdge { get; }

    public CursorTransitionEventArgs(string targetHostId, string targetHostname, double entryX, double entryY, ScreenEdge edge)
    {
        TargetHostId = targetHostId;
        TargetHostname = targetHostname;
        EntryX = entryX;
        EntryY = entryY;
        TransitionEdge = edge;
    }
}

/// <summary>
/// Desktop-to-desktop switching logic: edge collision, edge resistance, screen lock and entry-point calculation.
/// Pure state machine; the platform capture layer performs suppression, key release and cursor warping.
/// </summary>
public sealed class VirtualKvmManager
{
    private readonly object _lock = new();

    public ScreenBounds? LocalScreen { get; private set; }
    public IReadOnlyList<Monitor> LocalMonitors { get; private set; } = Array.Empty<Monitor>();
    public List<ScreenBounds> RemoteScreens { get; } = new();
    private readonly Dictionary<string, IReadOnlyList<Monitor>> _remotePlacedMonitors = new();
    public KeyTranslationService KeyTranslator { get; } = new();

    public bool IsScreenLocked { get; set; }
    public bool IsRedirected { get; private set; }
    public string? CurrentFocusedHostId { get; private set; }
    public bool ProportionalEntry { get; set; } = true;

    // Edge Resistance settings (prevents accidental boundary crossing)
    public int EdgeResistanceMs { get; set; } = 80;
    public double EdgePushDistanceThreshold { get; set; } = 15.0;

    private ScreenEdge _activeCollisionEdge = ScreenEdge.None;
    private DateTime _collisionStartTime = DateTime.MinValue;
    private double _accumulatedEdgePush;

    public event EventHandler<CursorTransitionEventArgs>? CursorTransitionedToRemote;
    public event EventHandler? ReturnedToLocalScreen;
    public event EventHandler<bool>? ScreenLockChanged;

    public VirtualKvmManager(ScreenBounds? localScreen = null)
    {
        LocalScreen = localScreen;
        CurrentFocusedHostId = localScreen?.HostId ?? "local";
    }

    public void SetLocalScreen(ScreenBounds screen)
    {
        lock (_lock)
        {
            LocalScreen = screen;
            if (!IsRedirected)
            {
                CurrentFocusedHostId = screen.HostId;
            }
        }
    }

    public void SetLocalMonitors(IReadOnlyList<Monitor> monitors)
    {
        lock (_lock)
        {
            LocalMonitors = monitors ?? Array.Empty<Monitor>();
            if (LocalMonitors.Count > 0)
            {
                double minX = LocalMonitors.Min(m => m.X);
                double minY = LocalMonitors.Min(m => m.Y);
                double maxX = LocalMonitors.Max(m => m.Right);
                double maxY = LocalMonitors.Max(m => m.Bottom);
                LocalScreen = new ScreenBounds
                {
                    HostId = LocalScreen?.HostId ?? "local",
                    Hostname = LocalScreen?.Hostname ?? Environment.MachineName,
                    X = minX,
                    Y = minY,
                    Width = maxX - minX,
                    Height = maxY - minY
                };
                if (!IsRedirected)
                {
                    CurrentFocusedHostId = LocalScreen.HostId;
                }
            }
        }
    }

    public void AddRemoteScreen(ScreenBounds screen, IReadOnlyList<Monitor>? monitors = null)
    {
        lock (_lock)
        {
            RemoteScreens.RemoveAll(s => s.HostId == screen.HostId);
            RemoteScreens.Add(screen);
            if (monitors != null && monitors.Count > 0)
            {
                _remotePlacedMonitors[screen.HostId] = monitors;
            }
            else
            {
                _remotePlacedMonitors[screen.HostId] = new[] { new Monitor(screen.X, screen.Y, screen.Width, screen.Height) };
            }
        }
    }

    public IReadOnlyList<Monitor> GetPlacedMonitors(string hostId)
    {
        lock (_lock)
        {
            return _remotePlacedMonitors.TryGetValue(hostId, out var monitors) ? monitors : Array.Empty<Monitor>();
        }
    }

    public void RemoveRemoteScreen(string hostId)
    {
        lock (_lock)
        {
            RemoteScreens.RemoveAll(s => s.HostId == hostId);
            _remotePlacedMonitors.Remove(hostId);
            if (CurrentFocusedHostId == hostId)
            {
                SwitchBackToLocal();
            }
        }
    }

    public void ToggleScreenLock()
    {
        IsScreenLocked = !IsScreenLocked;
        ScreenLockChanged?.Invoke(this, IsScreenLocked);
    }

    public void ForceRedirect(string hostId)
    {
        lock (_lock)
        {
            IsRedirected = true;
            CurrentFocusedHostId = hostId;
            _activeCollisionEdge = ScreenEdge.None;
            _accumulatedEdgePush = 0;
        }
    }

    /// <summary>
    /// Processes local mouse movement and checks if cursor crossed an active boundary.
    /// Returns true if a boundary transition occurred.
    /// </summary>
    public bool ProcessLocalMotion(double cursorX, double cursorY, short deltaX, short deltaY)
    {
        if (IsScreenLocked || LocalScreen == null || IsRedirected)
        {
            return false;
        }

        lock (_lock)
        {
            ScreenEdge hitEdge = DetectScreenEdgeCollision(cursorX, cursorY, deltaX, deltaY);

            if (hitEdge == ScreenEdge.None)
            {
                // Reset edge resistance counters
                _activeCollisionEdge = ScreenEdge.None;
                _accumulatedEdgePush = 0;
                return false;
            }

            if (LocalMonitors.Count > 0)
            {
                double checkX = Math.Clamp(cursorX, LocalScreen.X, LocalScreen.Right);
                double checkY = Math.Clamp(cursorY, LocalScreen.Y, LocalScreen.Bottom);

                if (!EdgeMap.IsOuterEdge(LocalMonitors, checkX, checkY, hitEdge))
                {
                    _activeCollisionEdge = ScreenEdge.None;
                    _accumulatedEdgePush = 0;
                    return false;
                }

                // Find candidate remote screens on this edge
                var candidates = RemoteScreens.Where(s => hitEdge switch
                {
                    ScreenEdge.Right => s.X >= LocalScreen.Right - 50,
                    ScreenEdge.Left => s.Right <= LocalScreen.X + 50,
                    ScreenEdge.Bottom => s.Y >= LocalScreen.Bottom - 50,
                    ScreenEdge.Top => s.Bottom <= LocalScreen.Y + 50,
                    _ => false
                }).ToList();

                ScreenBounds? targetScreen = null;
                (int PeerMonitorIndex, double EntryX, double EntryY)? crossing = null;

                foreach (var screen in candidates)
                {
                    var placed = _remotePlacedMonitors.TryGetValue(screen.HostId, out var pm) && pm.Count > 0
                        ? pm
                        : new[] { new Monitor(screen.X, screen.Y, screen.Width, screen.Height) };

                    var c = EdgeMap.FindCrossing(LocalMonitors, placed, checkX, checkY, hitEdge);
                    if (c != null)
                    {
                        targetScreen = screen;
                        crossing = c;
                        break;
                    }
                }

                if (targetScreen == null || crossing == null)
                {
                    _activeCollisionEdge = ScreenEdge.None;
                    _accumulatedEdgePush = 0;
                    return false;
                }

                if (CheckEdgeResistanceExceeded(hitEdge, deltaX, deltaY))
                {
                    ExecuteTransitionToRemote(targetScreen, cursorX, cursorY, hitEdge, crossing.Value.EntryX, crossing.Value.EntryY);
                    return true;
                }

                return false;
            }
            else
            {
                // Single-monitor / legacy fallback
                var targetScreen = FindAdjacentScreen(hitEdge);
                if (targetScreen == null)
                {
                    return false;
                }

                if (CheckEdgeResistanceExceeded(hitEdge, deltaX, deltaY))
                {
                    ExecuteTransitionToRemote(targetScreen, cursorX, cursorY, hitEdge);
                    return true;
                }

                return false;
            }
        }
    }

    public bool CheckEdgeResistanceExceeded(ScreenEdge hitEdge, short deltaX, short deltaY)
    {
        lock (_lock)
        {
            if (hitEdge == ScreenEdge.None)
            {
                _activeCollisionEdge = ScreenEdge.None;
                _accumulatedEdgePush = 0;
                return false;
            }

            if (_activeCollisionEdge != hitEdge)
            {
                _activeCollisionEdge = hitEdge;
                _collisionStartTime = DateTime.UtcNow;
                _accumulatedEdgePush = 0;
            }

            double pushDelta = hitEdge switch
            {
                ScreenEdge.Right => Math.Max(0, (int)deltaX),
                ScreenEdge.Left => Math.Max(0, -(int)deltaX),
                ScreenEdge.Bottom => Math.Max(0, (int)deltaY),
                ScreenEdge.Top => Math.Max(0, -(int)deltaY),
                _ => 0
            };

            _accumulatedEdgePush += pushDelta;
            var elapsedMs = (DateTime.UtcNow - _collisionStartTime).TotalMilliseconds;

            if (elapsedMs >= EdgeResistanceMs || _accumulatedEdgePush >= EdgePushDistanceThreshold)
            {
                _activeCollisionEdge = ScreenEdge.None;
                _accumulatedEdgePush = 0;
                return true;
            }

            return false;
        }
    }

    public void ResetEdgeResistance()
    {
        lock (_lock)
        {
            _activeCollisionEdge = ScreenEdge.None;
            _accumulatedEdgePush = 0;
        }
    }

    public ScreenEdge DetectScreenEdgeCollision(double x, double y, short dx, short dy)
    {
        if (LocalMonitors.Count > 0)
        {
            const double boundaryMargin = 3.0;
            double cx = LocalScreen != null ? Math.Clamp(x, LocalScreen.X, LocalScreen.Right) : x;
            double cy = LocalScreen != null ? Math.Clamp(y, LocalScreen.Y, LocalScreen.Bottom) : y;

            foreach (var m in LocalMonitors)
            {
                if (Math.Abs(cx - m.Right) <= boundaryMargin && dx > 0 && cy >= m.Y - boundaryMargin && cy <= m.Bottom + boundaryMargin)
                    return ScreenEdge.Right;
                if (Math.Abs(cx - m.X) <= boundaryMargin && dx < 0 && cy >= m.Y - boundaryMargin && cy <= m.Bottom + boundaryMargin)
                    return ScreenEdge.Left;
                if (Math.Abs(cy - m.Bottom) <= boundaryMargin && dy > 0 && cx >= m.X - boundaryMargin && cx <= m.Right + boundaryMargin)
                    return ScreenEdge.Bottom;
                if (Math.Abs(cy - m.Y) <= boundaryMargin && dy < 0 && cx >= m.X - boundaryMargin && cx <= m.Right + boundaryMargin)
                    return ScreenEdge.Top;
            }

            return ScreenEdge.None;
        }

        if (LocalScreen == null) return ScreenEdge.None;

        const double margin = 3.0;

        if (x >= LocalScreen.Right - margin && dx > 0)
            return ScreenEdge.Right;
        if (x <= LocalScreen.X + margin && dx < 0)
            return ScreenEdge.Left;
        if (y >= LocalScreen.Bottom - margin && dy > 0)
            return ScreenEdge.Bottom;
        if (y <= LocalScreen.Y + margin && dy < 0)
            return ScreenEdge.Top;

        return ScreenEdge.None;
    }

    public ScreenBounds? FindAdjacentScreen(ScreenEdge edge)
    {
        if (LocalScreen == null) return null;

        return edge switch
        {
            ScreenEdge.Right => RemoteScreens.FirstOrDefault(s => s.X >= LocalScreen.Right - 50),
            ScreenEdge.Left => RemoteScreens.FirstOrDefault(s => s.Right <= LocalScreen.X + 50),
            ScreenEdge.Bottom => RemoteScreens.FirstOrDefault(s => s.Y >= LocalScreen.Bottom - 50),
            ScreenEdge.Top => RemoteScreens.FirstOrDefault(s => s.Bottom <= LocalScreen.Y + 50),
            _ => null
        };
    }

    private void ExecuteTransitionToRemote(ScreenBounds target, double exitX, double exitY, ScreenEdge edge,
        double? overrideEntryX = null, double? overrideEntryY = null)
    {
        IsRedirected = true;
        CurrentFocusedHostId = target.HostId;
        _activeCollisionEdge = ScreenEdge.None;
        _accumulatedEdgePush = 0;

        double entryX, entryY;
        if (!ProportionalEntry && overrideEntryX.HasValue && overrideEntryY.HasValue)
        {
            entryX = overrideEntryX.Value;
            entryY = overrideEntryY.Value;
        }
        else
        {
            var (cx, cy) = CoordinateTransformationEngine.CalculateEntryPosition(
                exitX, exitY, LocalScreen!, target, edge, ProportionalEntry);
            entryX = cx;
            entryY = cy;
        }

        CursorTransitionedToRemote?.Invoke(this, new CursorTransitionEventArgs(
            target.HostId, target.Hostname, entryX, entryY, edge));
    }

    /// <summary>
    /// Returns focus to the local machine. The platform layer listening to <see cref="ReturnedToLocalScreen"/>
    /// stops suppressing local input and warps the cursor.
    /// </summary>
    public void SwitchBackToLocal()
    {
        lock (_lock)
        {
            IsRedirected = false;
            CurrentFocusedHostId = LocalScreen?.HostId ?? "local";
            ReturnedToLocalScreen?.Invoke(this, EventArgs.Empty);
        }
    }
}
