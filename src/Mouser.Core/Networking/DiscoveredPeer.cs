using System.Net;

namespace Mouser.Core.Networking;

/// <summary>
/// Represents a Mouser peer discovered on the local network.
/// </summary>
public sealed class DiscoveredPeer
{
    public string DeviceId { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public IPAddress IpAddress { get; set; } = IPAddress.Loopback;
    public int MotionPort { get; set; }
    public int ControlPort { get; set; }
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Bounding box of the peer's whole desktop in its own pixel coordinates (0 size if unknown).</summary>
    public int ScreenX { get; set; }
    public int ScreenY { get; set; }
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }

    public IPEndPoint MotionEndPoint => new IPEndPoint(IpAddress, MotionPort);
    public IPEndPoint ControlEndPoint => new IPEndPoint(IpAddress, ControlPort);

    public bool IsExpired(TimeSpan timeout) => (DateTime.UtcNow - LastSeenUtc) > timeout;
}
