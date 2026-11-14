using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Mouser.Core.Clipboard;
using Mouser.Core.Networking;
using Mouser.Core.Security;
using Mouser.UI.Services;
using Xunit;

namespace Mouser.Tests;

public sealed class FuzzTests
{
    [Fact]
    public void Fuzz_InputMotionPacket_TryRead_NeverThrows_NoHang()
    {
        var rng = new Random(1234);
        for (int i = 0; i < 2000; i++)
        {
            int len = rng.Next(0, 128);
            var buf = new byte[len];
            rng.NextBytes(buf);

            _ = InputMotionPacket.TryRead(buf, out _);
        }
    }

    [Fact]
    public void Fuzz_SecureInputPacket_TryRead_NeverThrows_NoHang()
    {
        var rng = new Random(1234);
        for (int i = 0; i < 2000; i++)
        {
            int len = rng.Next(0, 128);
            var buf = new byte[len];
            rng.NextBytes(buf);

            _ = SecureInputPacket.TryRead(buf, out _, out _, out _, out _);
        }
    }

    [Fact]
    public void Fuzz_PeerDiscoveryService_TryParseBeaconPayload_NeverThrows_NoHang()
    {
        var rng = new Random(1234);
        for (int i = 0; i < 2000; i++)
        {
            int len = rng.Next(0, 512);
            var buf = new byte[len];
            rng.NextBytes(buf);

            if (len >= 10 && rng.Next(2) == 0)
            {
                byte[] prefix = System.Text.Encoding.UTF8.GetBytes("MOUS_DISC:");
                Array.Copy(prefix, 0, buf, 0, prefix.Length);
            }

            _ = PeerDiscoveryService.TryParseBeaconPayload(buf, out _);
        }
    }

    [Fact]
    public async Task Fuzz_TcpFrameReader_ThrowsOnlyExpectedExceptions_NoHang()
    {
        var rng = new Random(1234);
        using var service = new PeerTransferService("fuzz-receiver", (_, _) => Array.Empty<byte[]>(), Path.GetTempPath(), 0);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        for (int i = 0; i < 2000; i++)
        {
            int len = rng.Next(0, 1024);
            var buf = new byte[len];
            rng.NextBytes(buf);

            if (len >= 6 && rng.Next(2) == 0)
            {
                buf[0] = (byte)'M';
                buf[1] = (byte)'C';
                buf[2] = (byte)'H';
                buf[3] = (byte)'1';
            }

            using var ms = new MemoryStream(buf);
            try
            {
                _ = await service.ReadFrameAsync(ms, IPAddress.Loopback, null, cts.Token);
            }
            catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or CryptographicException)
            {
                // Expected parsing/auth exceptions
            }
        }
    }

    [Fact]
    public void Fuzz_ClipboardPayload_Deserialize_ThrowsOnlyExpectedExceptions_NoHang()
    {
        var rng = new Random(1234);
        for (int i = 0; i < 2000; i++)
        {
            int len = rng.Next(0, 512);
            var buf = new byte[len];
            rng.NextBytes(buf);

            try
            {
                var payload = ClipboardPayload.Deserialize(buf);
                Assert.NotNull(payload);
            }
            catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or FormatException)
            {
                // Expected deserialization failure
            }
        }
    }

    [Fact]
    public void Fuzz_SearchQuery_Parse_NeverThrows_NoHang()
    {
        var rng = new Random(1234);
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 :\"'\\/!@#$%^&*()_+=-[]{}|;<>?,.~`\n\r\t\0";
        for (int i = 0; i < 2000; i++)
        {
            int len = rng.Next(0, 100);
            var chars = new char[len];
            for (int c = 0; c < len; c++)
            {
                chars[c] = alphabet[rng.Next(alphabet.Length)];
            }
            string input = new string(chars);

            var query = SearchQuery.Parse(input);
            Assert.NotNull(query);
        }
    }

    [Fact]
    public void BeaconSpoofing_DifferentIp_DoesNotHijackLink_UntilAuthenticated()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mouser_fuzz_spoof_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var store = new SettingsStore(Path.Combine(tempDir, "settings.json"));
            string peerId = "peer-legit-123";
            byte[] pairKey = new byte[32];
            RandomNumberGenerator.Fill(pairKey);

            var peerSettings = new PeerSettings
            {
                PairKeyProtected = SecretStore.Protect(pairKey)
            };
            store.Current.Peers[peerId] = peerSettings;
            store.Save();

            using var host = new MouserHost(store, new RecordingSink(), discoveryPort: 0, downloadDirectory: tempDir);

            // 1. Initial beacon from legitimate IP (192.168.1.50)
            var legitPeer = new DiscoveredPeer
            {
                DeviceId = peerId,
                Hostname = "Legit-PC",
                IpAddress = IPAddress.Parse("192.168.1.50"),
                MotionPort = 58491,
                ControlPort = 58492
            };
            var initialLink = host.EnsureLink(legitPeer);
            Assert.NotNull(initialLink);
            Assert.Equal(IPAddress.Parse("192.168.1.50"), initialLink.Target.Address);

            // 2. Spoofed beacon claiming the same DeviceId from attacker IP (192.168.1.99)
            var spoofedPeer = new DiscoveredPeer
            {
                DeviceId = peerId,
                Hostname = "Attacker-PC",
                IpAddress = IPAddress.Parse("192.168.1.99"),
                MotionPort = 58491,
                ControlPort = 58492
            };
            var currentLink = host.EnsureLink(spoofedPeer);

            // Must NOT hijack: link still points to legitimate IP
            Assert.Same(initialLink, currentLink);
            Assert.Equal(IPAddress.Parse("192.168.1.50"), currentLink.Target.Address);

            // 3. Legitimate roaming / authentication from the new endpoint
            host.MarkPeerAuthenticated(peerId, new IPEndPoint(IPAddress.Parse("192.168.1.99"), 58491));

            // Now EnsureLink allows the transition
            var updatedLink = host.EnsureLink(spoofedPeer);
            Assert.Equal(IPAddress.Parse("192.168.1.99"), updatedLink.Target.Address);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
