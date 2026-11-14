using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Mouser.Core.Networking;
using Mouser.Core.Services;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public class WakeOnLanTests
{
    [Fact]
    public void BuildMagicPacket_Produces102BytesStartingWithSixFFAndSixteenMacRepetitions()
    {
        byte[] mac = [0x00, 0x11, 0x22, 0x33, 0x44, 0x55];
        byte[] packet = WakeOnLan.BuildMagicPacket(mac);

        Assert.Equal(102, packet.Length);

        // First 6 bytes must be 0xFF
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(0xFF, packet[i]);
        }

        // Followed by 16 repetitions of mac
        for (int rep = 0; rep < 16; rep++)
        {
            for (int b = 0; b < 6; b++)
            {
                Assert.Equal(mac[b], packet[6 + rep * 6 + b]);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(12)]
    public void BuildMagicPacket_ThrowsForInvalidLength(int length)
    {
        byte[] mac = new byte[length];
        Assert.Throws<ArgumentException>(() => WakeOnLan.BuildMagicPacket(mac));
    }

    [Fact]
    public void BuildMagicPacket_ThrowsForNull()
    {
        Assert.Throws<ArgumentException>(() => WakeOnLan.BuildMagicPacket(null!));
    }

    [Theory]
    [InlineData("aa:bb:cc:dd:ee:ff")]
    [InlineData("AA:BB:CC:DD:EE:FF")]
    [InlineData("aa-bb-cc-dd-ee-ff")]
    [InlineData("AA-BB-CC-DD-EE-FF")]
    [InlineData("aabbccddeeff")]
    [InlineData("AABBCCDDEEFF")]
    [InlineData("aA:bB-cC:dD-eE:fF")]
    [InlineData("  aa:bb:cc:dd:ee:ff  ")]
    public void TryParseMac_SucceedsForVariousDelimitersAndCases(string input)
    {
        bool ok = WakeOnLan.TryParseMac(input, out var mac);

        Assert.True(ok);
        Assert.NotNull(mac);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF }, mac);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("aa:bb:cc:dd:ee")]          // 5 parts
    [InlineData("aa:bb:cc:dd:ee:ff:00")]     // 7 parts
    [InlineData("aabbccddeef")]              // 11 hex chars
    [InlineData("aabbccddeeff00")]           // 14 hex chars
    [InlineData("gg:hh:ii:jj:kk:ll")]        // invalid hex chars
    [InlineData("00:11:22:33:44:zz")]        // invalid hex char at end
    [InlineData("0011223344zz")]              // invalid hex char without delimiter
    [InlineData("aa::bb:cc:dd:ee:ff")]       // double delimiter
    [InlineData("a:b:c:d:e:f")]              // 1 digit per part
    public void TryParseMac_FailsForInvalidInputs(string? input)
    {
        bool ok = WakeOnLan.TryParseMac(input, out var mac);

        Assert.False(ok);
        Assert.Null(mac);
    }

    [Fact]
    public void FormatMac_ProducesLowercaseColonDelimitedHex()
    {
        byte[] mac = [0x00, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E];
        string formatted = WakeOnLan.FormatMac(mac);

        Assert.Equal("00:1a:2b:3c:4d:5e", formatted);
    }

    [Fact]
    public void FormatMac_ThrowsForInvalidMac()
    {
        Assert.Throws<ArgumentException>(() => WakeOnLan.FormatMac(null!));
        Assert.Throws<ArgumentException>(() => WakeOnLan.FormatMac(new byte[5]));
        Assert.Throws<ArgumentException>(() => WakeOnLan.FormatMac(new byte[7]));
    }

    [Fact]
    public void FormatMac_RoundTripsWithTryParseMac()
    {
        byte[] original = [0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC];
        string formatted = WakeOnLan.FormatMac(original);

        bool parsed = WakeOnLan.TryParseMac(formatted, out var roundTrip);

        Assert.True(parsed);
        Assert.Equal(original, roundTrip);
    }

    [Fact]
    public async Task SendAsync_RunsWithoutThrowingForValidMac()
    {
        byte[] mac = [0x00, 0x11, 0x22, 0x33, 0x44, 0x55];
        var exception = await Record.ExceptionAsync(() => WakeOnLan.SendAsync(mac, 9));
        Assert.Null(exception);
    }

    [Fact]
    public void PeerSettings_MacProperty_DefaultsToEmpty_AndPersists()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "mouser-wol-test-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new SettingsStore(tmp);
            var peer = new PeerSettings { Name = "Desktop", Mac = "00:11:22:33:44:55" };
            store.Current.Peers["test-peer"] = peer;
            store.Save();

            var loaded = new SettingsStore(tmp);
            Assert.True(loaded.Current.Peers.TryGetValue("test-peer", out var saved));
            Assert.Equal("00:11:22:33:44:55", saved.Mac);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void PeerViewModel_CanWake_ReflectsOnlineAndMacState()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "mouser-wol-test-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new SettingsStore(tmp);
            store.Current.Peers["peer-1"] = new PeerSettings { Name = "PC1", Mac = "aa:bb:cc:dd:ee:ff" };
            store.Current.Peers["peer-2"] = new PeerSettings { Name = "PC2", Mac = "" };

            var layoutVm = new MonitorLayoutViewModel(settings: store);

            var peer1 = new DiscoveredPeer { DeviceId = "peer-1", Hostname = "PC1" };
            var peerVm1 = new PeerViewModel(layoutVm, peer1, PeerLinkStatus.Unreachable, Mouser.Core.Kvm.PeerPlacement.None);

            Assert.False(peerVm1.IsOnline);
            Assert.Equal("aa:bb:cc:dd:ee:ff", peerVm1.Mac);
            Assert.True(peerVm1.CanWake);

            // If peer becomes online, CanWake is false
            peerVm1.Status = PeerLinkStatus.Paired;
            Assert.True(peerVm1.IsOnline);
            Assert.False(peerVm1.CanWake);

            // Peer 2 with no MAC
            var peer2 = new DiscoveredPeer { DeviceId = "peer-2", Hostname = "PC2" };
            var peerVm2 = new PeerViewModel(layoutVm, peer2, PeerLinkStatus.Unreachable, Mouser.Core.Kvm.PeerPlacement.None);
            Assert.False(peerVm2.IsOnline);
            Assert.Empty(peerVm2.Mac);
            Assert.False(peerVm2.CanWake);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void DeviceDetailViewModel_CanWake_ReflectsOnlineAndMacState()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "mouser-wol-test-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new SettingsStore(tmp);
            store.Current.Peers["dev-1"] = new PeerSettings { Name = "PC1", Mac = "aa:bb:cc:dd:ee:ff" };

            var vm = new DeviceDetailViewModel("dev-1", DeviceKind.Computer, store)
            {
                IsOnline = false
            };

            Assert.Equal("aa:bb:cc:dd:ee:ff", vm.Mac);
            Assert.True(vm.CanWake);

            vm.IsOnline = true;
            Assert.False(vm.CanWake);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void MonitorLayoutViewModel_SetPeers_RetainsSavedPeersAsUnreachable()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "mouser-wol-test-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new SettingsStore(tmp);
            store.Current.Peers["saved-1"] = new PeerSettings { Name = "SavedPC", Mac = "aa:bb:cc:dd:ee:ff" };

            var vm = new MonitorLayoutViewModel(settings: store);
            vm.SetPeers(Array.Empty<DiscoveredPeer>());

            Assert.Single(vm.Peers);
            var retained = vm.Peers[0];
            Assert.Equal("saved-1", retained.Id);
            Assert.Equal("SavedPC", retained.Hostname);
            Assert.Equal(PeerLinkStatus.Unreachable, retained.Status);
            Assert.True(retained.CanWake);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public async Task MouserHost_WakePeerAsync_SendsPacketAndLogsEvent()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "mouser-wol-test-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new SettingsStore(tmp);
            store.Current.Peers["target-pc"] = new PeerSettings { Name = "TargetPC", Mac = "00:11:22:33:44:55" };

            using var host = new MouserHost(store, new RecordingSink(), discoveryPort: 0);
            await host.WakePeerAsync("target-pc");

            var events = host.Log.Snapshot();
            Assert.Contains(events, e => e.Category == ActivityCategory.Network && e.Message.Contains("Wake-on-LAN magic packet") && e.Message.Contains("TargetPC"));
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
