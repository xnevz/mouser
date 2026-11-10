using System.Net;
using Mouser.Core.Networking;
using Xunit;

namespace Mouser.Tests;

public class PeerDiscoveryTests
{
    [Fact]
    public void BeaconPayload_Serialization_RoundTripsAccurately()
    {
        var original = new DiscoveredPeer
        {
            DeviceId = "test-device-uuid-1234",
            Hostname = "Workstation Pro",
            OperatingSystem = "macOS",
            MotionPort = 58491,
            ControlPort = 58492
        };

        byte[] payload = PeerDiscoveryService.CreateBeaconPayload(original);
        Assert.NotEmpty(payload);

        bool success = PeerDiscoveryService.TryParseBeaconPayload(payload, out var parsed);
        Assert.True(success);
        Assert.Equal(original.DeviceId, parsed.DeviceId);
        Assert.Equal(original.Hostname, parsed.Hostname);
        Assert.Equal(original.OperatingSystem, parsed.OperatingSystem);
        Assert.Equal(original.MotionPort, parsed.MotionPort);
        Assert.Equal(original.ControlPort, parsed.ControlPort);
    }

    [Fact]
    public void TryParseBeaconPayload_RejectsInvalidPrefix()
    {
        byte[] invalidBytes = System.Text.Encoding.UTF8.GetBytes("INVALID_DATA:{}");
        bool success = PeerDiscoveryService.TryParseBeaconPayload(invalidBytes, out _);
        Assert.False(success);
    }

    [Fact]
    public void DiscoveredPeer_IsExpired_ReturnsTrueAfterTimeout()
    {
        var peer = new DiscoveredPeer
        {
            DeviceId = "expiring-peer",
            LastSeenUtc = DateTime.UtcNow.AddSeconds(-10)
        };

        Assert.True(peer.IsExpired(TimeSpan.FromSeconds(5)));
        Assert.False(peer.IsExpired(TimeSpan.FromSeconds(20)));
    }
}
