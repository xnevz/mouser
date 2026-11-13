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

    [Theory]
    [InlineData("192.168.1.37", "255.255.255.0", "192.168.1.255")]
    [InlineData("10.4.20.9", "255.255.0.0", "10.4.255.255")]
    [InlineData("172.16.5.130", "255.255.255.128", "172.16.5.255")]
    public void DirectedBroadcast_IsComputedFromSubnetMask(string ip, string mask, string expected)
    {
        var result = NetworkInfo.GetDirectedBroadcast(IPAddress.Parse(ip), IPAddress.Parse(mask));
        Assert.Equal(IPAddress.Parse(expected), result);
    }

    [Fact]
    public void BroadcastAddresses_AlwaysIncludeLimitedBroadcast()
    {
        Assert.Contains(IPAddress.Broadcast, NetworkInfo.GetBroadcastAddresses());
    }

    [Fact]
    public async Task TwoHosts_OnTheSameMachine_DiscoverEachOther()
    {
        int port = Random.Shared.Next(40_000, 50_000);
        using var a = new PeerDiscoveryService("host-a", "Alpha", "Windows", 58491, 0, port);
        using var b = new PeerDiscoveryService("host-b", "Beta", "macOS", 59000, 0, port);
        a.Start();
        b.Start();

        await Wait.UntilAsync(() => a.ActivePeers.Any(p => p.DeviceId == "host-b"), timeoutMs: 5000);

        var peer = a.ActivePeers.Single(p => p.DeviceId == "host-b");
        Assert.Equal("Beta", peer.Hostname);
        Assert.Equal(59000, peer.MotionPort);
        Assert.DoesNotContain(a.ActivePeers, p => p.DeviceId == "host-a"); // own beacon ignored
    }

    [Fact]
    public async Task PeerAddress_StaysStable_WhenBeaconsArriveFromSeveralAdapters()
    {
        int port = Random.Shared.Next(40_000, 50_000);
        using var service = new PeerDiscoveryService("me", "Me", "Windows", 1, 0, port);
        service.Start();
        byte[] beacon = PeerDiscoveryService.CreateBeaconPayload(new DiscoveredPeer { DeviceId = "other", Hostname = "Other", MotionPort = 58491 });

        // Same beacon from two different local addresses, like a PC with Ethernet plus a Hyper-V adapter.
        using var first = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        first.Send(beacon, beacon.Length, new IPEndPoint(IPAddress.Loopback, port));
        await Wait.UntilAsync(() => service.ActivePeers.Any(p => p.DeviceId == "other"));
        var firstAddress = service.ActivePeers.Single(p => p.DeviceId == "other").IpAddress;

        using var second = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Parse("127.0.0.2"), 0));
        second.Send(beacon, beacon.Length, new IPEndPoint(IPAddress.Loopback, port));
        await Task.Delay(200);

        Assert.Equal(firstAddress, service.ActivePeers.Single(p => p.DeviceId == "other").IpAddress);
    }
}
