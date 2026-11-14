using System;
using System.Collections.Generic;
using Mouser.Core.Geometry;

namespace Mouser.Core.Kvm;

/// <summary>
/// Pure virtual plane coordinate space for multi-computer chains (Task E11).
/// Places multiple computers in a unified 2D plane where each computer occupies
/// a non-overlapping rectangle. When the cursor leaves one screen into an adjacent
/// computer's rectangle, control seamlessly transitions. Gaps between rectangles stop the cursor.
/// </summary>
public sealed class VirtualPlane
{
    private readonly Dictionary<string, ScreenBounds> _screens = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ScreenBounds> Screens => _screens;

    public void Add(string id, ScreenBounds bounds)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Screen id cannot be null or empty", nameof(id));

        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentException("Screen bounds must have positive width and height", nameof(bounds));

        if (_screens.ContainsKey(id))
            throw new InvalidOperationException($"Screen '{id}' is already registered in the virtual plane.");

        // Reject overlapping rectangles on insert (E11 requirement)
        foreach (var (existingId, existing) in _screens)
        {
            if (Overlaps(bounds, existing))
            {
                throw new InvalidOperationException(
                    $"Screen '{id}' {bounds} overlaps with existing screen '{existingId}' {existing}.");
            }
        }

        _screens[id] = bounds;
    }

    public bool Remove(string id) => _screens.Remove(id);

    public void Clear() => _screens.Clear();

    public bool TryGet(string id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ScreenBounds? bounds) => _screens.TryGetValue(id, out bounds);

    /// <summary>
    /// Moves the cursor by (dx, dy) starting from (currentX, currentY) in currentId's local screen coordinates.
    /// Returns the target screen id and resulting local coordinates.
    /// If the move would exit into empty space (a gap), the cursor stops at the screen boundary.
    /// </summary>
    public (string ScreenId, double X, double Y) Move(string currentId, double currentX, double currentY, double dx, double dy)
    {
        if (!_screens.TryGetValue(currentId, out var currentBounds))
            throw new KeyNotFoundException($"Screen '{currentId}' is not found in the virtual plane.");

        double globalX = currentBounds.X + currentX + dx;
        double globalY = currentBounds.Y + currentY + dy;

        // Check if still within current screen
        if (ContainsPoint(currentBounds, globalX, globalY))
        {
            return (currentId, globalX - currentBounds.X, globalY - currentBounds.Y);
        }

        // Look for adjacent screen containing the target point
        foreach (var (peerId, peerBounds) in _screens)
        {
            if (peerId.Equals(currentId, StringComparison.OrdinalIgnoreCase)) continue;

            if (ContainsPoint(peerBounds, globalX, globalY))
            {
                return (peerId, globalX - peerBounds.X, globalY - peerBounds.Y);
            }
        }

        // If no screen contains the target point, it hit a gap or outer boundary: stop cursor at edge
        double clampedGlobalX = Math.Clamp(globalX, currentBounds.X, currentBounds.Right - 1);
        double clampedGlobalY = Math.Clamp(globalY, currentBounds.Y, currentBounds.Bottom - 1);

        return (currentId, clampedGlobalX - currentBounds.X, clampedGlobalY - currentBounds.Y);
    }

    private static bool ContainsPoint(ScreenBounds bounds, double x, double y)
    {
        return x >= bounds.X && x < bounds.Right && y >= bounds.Y && y < bounds.Bottom;
    }

    private static bool Overlaps(ScreenBounds a, ScreenBounds b)
    {
        // Two rectangles overlap if they intersect with non-zero area
        return a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;
    }
}
