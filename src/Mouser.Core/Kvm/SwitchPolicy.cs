using System;
using Mouser.Core.Geometry;

namespace Mouser.Core.Kvm;

public readonly record struct SwitchContext(
    bool ForegroundFullscreen, bool ForegroundMaximized,
    double CursorX, double CursorY, ScreenBounds Local,
    bool CtrlDown, bool AltDown, bool ShiftDown, DateTime Now);

public sealed class SwitchPolicy
{
    private readonly KvmOptions _options;
    private DateTime _lastDenied = DateTime.MinValue;
    private DateTime _lastAttempt = DateTime.MinValue;
    private bool _wasInEdgeZone;

    public SwitchPolicy(KvmOptions options)
    {
        _options = options;
    }

    /// <summary>Resets the edge zone tracking state when the cursor moves away from the edge.</summary>
    public void ResetEdgeZone()
    {
        _wasInEdgeZone = false;
    }

    /// <summary>True when the cursor may leave through this edge now.</summary>
    public bool Allows(in SwitchContext c)
    {
        bool denied = false;

        if (_options.BlockInFullscreen && c.ForegroundFullscreen) denied = true;
        else if (_options.BlockInMaximized && c.ForegroundMaximized) denied = true;
        else if (_options.BlockInCorners && IsNearCorner(c.CursorX, c.CursorY, c.Local)) denied = true;
        else if (!string.IsNullOrEmpty(_options.RequireModifier) && !_options.RequireModifier.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            bool modDown = _options.RequireModifier.ToLowerInvariant() switch
            {
                "ctrl" => c.CtrlDown,
                "alt" => c.AltDown,
                "shift" => c.ShiftDown,
                _ => true
            };
            if (!modDown) denied = true;
        }

        if (denied)
        {
            bool isNewContact = !_wasInEdgeZone || (c.Now - _lastAttempt).TotalMilliseconds >= 30;
            double msSinceLastDenied = (c.Now - _lastDenied).TotalMilliseconds;

            if (_options.DoubleTapOverride && isNewContact && msSinceLastDenied <= 400 && msSinceLastDenied >= 30)
            {
                // Double-tap override allows crossing!
                _wasInEdgeZone = true;
                _lastAttempt = c.Now;
                return true;
            }

            if (isNewContact)
            {
                _lastDenied = c.Now;
            }
            _lastAttempt = c.Now;
            _wasInEdgeZone = true;
            return false;
        }

        _wasInEdgeZone = false;
        _lastAttempt = c.Now;
        return true;
    }

    private static bool IsNearCorner(double x, double y, ScreenBounds local)
    {
        // within 80 px of any corner: |dx| < 80 AND |dy| < 80
        const double threshold = 80.0;
        // Top-left
        if (Math.Abs(x - local.X) < threshold && Math.Abs(y - local.Y) < threshold) return true;
        // Top-right
        if (Math.Abs(x - local.Right) < threshold && Math.Abs(y - local.Y) < threshold) return true;
        // Bottom-left
        if (Math.Abs(x - local.X) < threshold && Math.Abs(y - local.Bottom) < threshold) return true;
        // Bottom-right
        if (Math.Abs(x - local.Right) < threshold && Math.Abs(y - local.Bottom) < threshold) return true;
        return false;
    }
}
