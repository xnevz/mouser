using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Mouser.Core.Networking;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public sealed class NetworkExposureTests
{
    [Theory]
    // Loopback
    [InlineData("127.0.0.1", true)]
    [InlineData("127.123.45.67", true)]
    [InlineData("::1", true)]
    // 10.0.0.0/8
    [InlineData("10.0.0.1", true)]
    [InlineData("10.254.1.99", true)]
    // 172.16.0.0/12
    [InlineData("172.16.0.1", true)]
    [InlineData("172.24.50.60", true)]
    [InlineData("172.31.255.254", true)]
    [InlineData("172.15.255.255", false)]
    [InlineData("172.32.0.1", false)]
    // 192.168.0.0/16
    [InlineData("192.168.0.1", true)]
    [InlineData("192.168.100.200", true)]
    // 169.254.0.0/16 (link-local)
    [InlineData("169.254.1.1", true)]
    [InlineData("169.254.254.254", true)]
    // 100.64.0.0/10 (CGNAT / Tailscale)
    [InlineData("100.64.0.1", true)]
    [InlineData("100.100.100.100", true)]
    [InlineData("100.127.255.254", true)]
    [InlineData("100.63.255.255", false)]
    [InlineData("100.128.0.1", false)]
    // IPv6 Private / Link-local
    [InlineData("fd00::1", true)]
    [InlineData("fc00::abcd", true)]
    [InlineData("fe80::1", true)]
    // Public IPv4 & IPv6
    [InlineData("8.8.8.8", false)]
    [InlineData("1.1.1.1", false)]
    [InlineData("203.0.113.45", false)]
    [InlineData("2607:f8b0:4005:805::200e", false)]
    // IPv4-mapped IPv6
    [InlineData("::ffff:192.168.1.1", true)]
    [InlineData("::ffff:8.8.8.8", false)]
    public void NetworkInfo_IsPrivate_IdentifiesCorrectly(string ipStr, bool expected)
    {
        var ip = IPAddress.Parse(ipStr);
        Assert.Equal(expected, NetworkInfo.IsPrivate(ip));
    }

    [Fact]
    public void NetworkInfo_IsPrivate_Null_ReturnsFalse()
    {
        Assert.False(NetworkInfo.IsPrivate(null));
    }

    [Fact]
    public void NetworkInfo_FindFreeUdpPort_ReturnsValidPort()
    {
        int port = NetworkInfo.FindFreeUdpPort();
        Assert.InRange(port, 1024, 65535);
    }

    [Fact]
    public void NetworkInfo_FindFreeTcpPort_ReturnsValidPort()
    {
        int port = NetworkInfo.FindFreeTcpPort();
        Assert.InRange(port, 1024, 65535);
    }

    [Fact]
    public void NetworkInfo_GetAdapters_ReturnsNonEmptyOrEmptyWithoutThrowing()
    {
        var adapters = NetworkInfo.GetAdapters();
        Assert.NotNull(adapters);
    }

    [Fact]
    public async Task UdpInputReceiver_LocalNetworkOnly_DropsNonPrivatePackets()
    {
        using var receiver = new UdpInputReceiver(0)
        {
            LocalNetworkOnly = true
        };
        bool packetReceived = false;
        receiver.DatagramReceived += (_, _) => packetReceived = true;
        receiver.Start();

        // Send datagram from loopback (private) -> delivered
        using var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        var targetEp = new IPEndPoint(IPAddress.Loopback, receiver.BoundPort);
        sender.SendTo(new byte[] { 1, 2, 3 }, targetEp);

        await Wait.UntilAsync(() => packetReceived, timeoutMs: 2000);
        Assert.True(packetReceived);

        // Verify IsPrivate check drops 8.8.8.8
        Assert.False(NetworkInfo.IsPrivate(IPAddress.Parse("8.8.8.8")));
    }

    [Fact]
    public async Task PeerTransferService_LocalNetworkOnly_DropsNonPrivateClients()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_transfer_net_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmp);
        try
        {
            using var service = new PeerTransferService("test-dev", (_, _) => Array.Empty<byte[]>(), tmp, 0)
            {
                LocalNetworkOnly = true
            };
            Assert.True(service.LocalNetworkOnly);

            // Connect from loopback -> accepted
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, service.BoundPort);
            Assert.True(client.Connected);
        }
        finally
        {
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        }
    }

    [Fact]
    public void SettingsViewModel_NetworkSettings_ToggleAndPersist()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_settings_net_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tmp);
            using var host = new MouserHost(store, new RecordingSink(), discoveryPort: 0);
            var vm = new SettingsViewModel(store, host);

            Assert.True(vm.LocalNetworkOnly);
            Assert.False(vm.UseIPv6);

            // Toggle LocalNetworkOnly
            vm.LocalNetworkOnly = false;
            Assert.False(store.Current.LocalNetworkOnly);

            // Toggle UseIPv6
            vm.UseIPv6 = true;
            Assert.True(store.Current.UseIPv6);

            // Port commands
            vm.FindFreeInputPortCommand.Execute(null);
            Assert.InRange(vm.InputPort, 1024, 65535);

            vm.FindFreeTcpPortCommand.Execute(null);
            Assert.InRange(vm.TcpPort, 1024, 65535);

            vm.ApplyPortsCommand.Execute(null);
            Assert.Equal(vm.InputPort, store.Current.InputPort);
            Assert.Equal(vm.TcpPort, store.Current.TcpPort);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void SettingsViewModel_ApplyPorts_RejectsOutOfRange()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_settings_bad_ports_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tmp);
            using var host = new MouserHost(store, new RecordingSink(), discoveryPort: 0);
            var vm = new SettingsViewModel(store, host);

            vm.InputPort = 500; // < 1024
            vm.ApplyPortsCommand.Execute(null);
            Assert.Contains("Choose an input UDP port between 1024 and 65535", vm.StatusMessage);

            vm.InputPort = 50000;
            vm.TcpPort = 70000; // > 65535
            vm.ApplyPortsCommand.Execute(null);
            Assert.Contains("Choose a transfer TCP port between 1024 and 65535", vm.StatusMessage);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
