using System;
using System.Collections.Generic;
using System.Linq;
using Mouser.Core.Geometry;

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
    public List<ScreenBounds> RemoteScreens { get; } = new();
    public KeyTranslationService KeyTranslator { get; } = new();

    public bool IsScreenLocked { get; set; }
    public bool IsRedirected { get; private set; }
    public string? CurrentFocusedHostId { get; private set; }

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

    public void AddRemoteScreen(ScreenBounds screen)
    {
        lock (_lock)
        {
            RemoteScreens.RemoveAll(s => s.HostId == screen.HostId);
            RemoteScreens.Add(screen);
        }
    }

    public void RemoveRemoteScreen(string hostId)
    {
        lock (_lock)
        {
            RemoteScreens.RemoveAll(s => s.HostId == hostId);
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

            // Find an adjacent remote screen matching this edge
            var targetScreen = FindAdjacentScreen(hitEdge);
            if (targetScreen == null)
            {
                return false;
            }

            // Check edge resistance dwell and push accumulation
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
                // Transition to remote screen!
                ExecuteTransitionToRemote(targetScreen, cursorX, cursorY, hitEdge);
                return true;
            }

            return false;
        }
    }

    private ScreenEdge DetectScreenEdgeCollision(double x, double y, short dx, short dy)
    {
        if (LocalScreen == null) return ScreenEdge.None;

        const double boundaryMargin = 3.0;

        if (x >= LocalScreen.Right - boundaryMargin && dx > 0)
            return ScreenEdge.Right;
        if (x <= LocalScreen.X + boundaryMargin && dx < 0)
            return ScreenEdge.Left;
        if (y >= LocalScreen.Bottom - boundaryMargin && dy > 0)
            return ScreenEdge.Bottom;
        if (y <= LocalScreen.Y + boundaryMargin && dy < 0)
            return ScreenEdge.Top;

        return ScreenEdge.None;
    }

    private ScreenBounds? FindAdjacentScreen(ScreenEdge edge)
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

    private void ExecuteTransitionToRemote(ScreenBounds target, double exitX, double exitY, ScreenEdge edge)
    {
        IsRedirected = true;
        CurrentFocusedHostId = target.HostId;
        _activeCollisionEdge = ScreenEdge.None;
        _accumulatedEdgePush = 0;

        // Calculate entry position on target screen
        var (entryX, entryY) = CoordinateTransformationEngine.CalculateEntryPosition(
            exitX, exitY, LocalScreen!, target, edge);

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
