using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Mouser.Core.Input;
using Mouser.Core.Networking;
using Mouser.Core.Security;
using Mouser.Core.Services;

namespace Mouser.Tests;

public sealed class SecureInputChannelTests : IDisposable
{
    private readonly RecordingSink _sinkB = new();
    private readonly PairingGate _gateB = new(123_456);
    private readonly InputInjectionService _hostB;
    private readonly byte[] _pairKey = new byte[32];

    public SecureInputChannelTests()
    {
        RandomNumberGenerator.Fill(_pairKey);
        _hostB = new InputInjectionService(_sinkB, _gateB, listenPort: 0, ownDeviceId: "device-b")
        {
            AllowOlderDevices = false
        };
        _hostB.RegisterPeer("device-a", _pairKey);
        _hostB.Start();
    }

    private IPEndPoint EndPointB => new(IPAddress.Loopback, _hostB.BoundPort);

    [Fact]
    public void SecureInputPacket_WriteAndTryRead_RoundTrip()
    {
        var senderHash = SecureInputPacket.ComputeSenderIdHash("my-device-id");
        ulong sessionId = 123456789UL;
        ulong counter = 42UL;
        var cipherAndTag = new byte[30];
        RandomNumberGenerator.Fill(cipherAndTag);

        var packetBytes = SecureInputPacket.Write(senderHash, sessionId, counter, cipherAndTag);

        Assert.Equal(51, packetBytes.Length);
        Assert.Equal(2, packetBytes[0]);

        bool success = SecureInputPacket.TryRead(packetBytes, out var readHash, out ulong readSession, out ulong readCounter, out var readCipher);
        Assert.True(success);
        Assert.Equal(senderHash, readHash);
        Assert.Equal(sessionId, readSession);
        Assert.Equal(counter, readCounter);
        Assert.Equal(cipherAndTag, readCipher);
    }

    [Fact]
    public void SecureInputPacket_TryRead_GarbageReturnsFalse()
    {
        // Truncated (50 bytes)
        var shortBuffer = new byte[50];
        shortBuffer[0] = 2;
        Assert.False(SecureInputPacket.TryRead(shortBuffer, out _, out _, out _, out _));

        // Oversized (52 bytes)
        var longBuffer = new byte[52];
        longBuffer[0] = 2;
        Assert.False(SecureInputPacket.TryRead(longBuffer, out _, out _, out _, out _));

        // Wrong version (version 1)
        var wrongVersion = new byte[51];
        wrongVersion[0] = 1;
        Assert.False(SecureInputPacket.TryRead(wrongVersion, out _, out _, out _, out _));
    }

    [Fact]
    public async Task EndToEnd_SecureInputChannel_TransfersMotionRelative()
    {
        using var linkA = new PeerLink(EndPointB, pairKey: _pairKey, ownDeviceId: "device-a", peerDeviceId: "device-b");

        // Wait until link is alive / ping acknowledged
        await Wait.UntilAsync(() => linkA.IsAlive, timeoutMs: 3000);

        linkA.Send(InputMotionPacket.TypeMotion, dx: 15, dy: -20, buttons: 0);

        await Wait.UntilAsync(() => _sinkB.Snapshot().Length > 0, timeoutMs: 3000);

        var events = _sinkB.Snapshot();
        Assert.Contains("move 15,-20", events);
    }

    [Fact]
    public async Task SpoofedSourceWithBadTag_IsIgnored()
    {
        using var udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // Craft a 51-byte packet with device-a's hash but corrupted cipher/tag
        var senderHash = SecureInputPacket.ComputeSenderIdHash("device-a");
        var badCipherAndTag = new byte[30];
        RandomNumberGenerator.Fill(badCipherAndTag);

        var badPacket = SecureInputPacket.Write(senderHash, sessionId: 9999, counter: 1, badCipherAndTag);
        udp.SendTo(badPacket, EndPointB);

        await Task.Delay(100);

        Assert.Empty(_sinkB.Snapshot());
        Assert.True(_hostB.RejectedPackets > 0);
    }

    [Fact]
    public async Task ReplayOfCapturedDatagram_IsIgnored()
    {
        ulong sessionId = 8888UL;
        bool isInitiator = string.CompareOrdinal("device-a", "device-b") < 0;
        using var senderCrypto = new SessionCrypto(_pairKey, isInitiator, sessionId);

        byte[] plaintext = new byte[14];
        InputMotionPacket.Create(InputMotionPacket.TypeMotion, 1, dx: 25, dy: 35, buttons: 0).TryWrite(plaintext);
        byte[] cipherAndTag = senderCrypto.Seal(plaintext, out ulong counter);

        var senderHash = SecureInputPacket.ComputeSenderIdHash("device-a");
        var datagram = SecureInputPacket.Write(senderHash, sessionId, counter, cipherAndTag);

        using var udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // First delivery: should be accepted
        udp.SendTo(datagram, EndPointB);
        await Wait.UntilAsync(() => _sinkB.Snapshot().Length == 1, timeoutMs: 3000);
        Assert.Equal("move 25,35", _sinkB.Snapshot()[0]);

        long rejectedBefore = _hostB.RejectedPackets;

        // Replay of exact same datagram: must be rejected
        udp.SendTo(datagram, EndPointB);
        await Task.Delay(100);

        Assert.Single(_sinkB.Snapshot());
        Assert.True(_hostB.RejectedPackets > rejectedBefore);
    }

    [Fact]
    public async Task SessionRestart_NewSessionId_Works()
    {
        bool isInitiator = string.CompareOrdinal("device-a", "device-b") < 0;
        var senderHash = SecureInputPacket.ComputeSenderIdHash("device-a");

        using var udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // Session 1000
        using (var crypto1 = new SessionCrypto(_pairKey, isInitiator, 1000))
        {
            byte[] pt1 = new byte[14];
            InputMotionPacket.Create(InputMotionPacket.TypeMotion, 1, dx: 10, dy: 10, buttons: 0).TryWrite(pt1);
            var ct1 = crypto1.Seal(pt1, out ulong c1);
            udp.SendTo(SecureInputPacket.Write(senderHash, 1000, c1, ct1), EndPointB);
        }

        await Wait.UntilAsync(() => _sinkB.Snapshot().Length == 1, timeoutMs: 3000);

        // Session 2000 (restarted session with higher sessionId)
        using (var crypto2 = new SessionCrypto(_pairKey, isInitiator, 2000))
        {
            byte[] pt2 = new byte[14];
            InputMotionPacket.Create(InputMotionPacket.TypeMotion, 1, dx: 40, dy: 40, buttons: 0).TryWrite(pt2);
            var ct2 = crypto2.Seal(pt2, out ulong c2);
            udp.SendTo(SecureInputPacket.Write(senderHash, 2000, c2, ct2), EndPointB);
        }

        await Wait.UntilAsync(() => _sinkB.Snapshot().Length == 2, timeoutMs: 3000);
        Assert.Equal("move 40,40", _sinkB.Snapshot()[1]);
    }

    [Fact]
    public async Task OldSessionId_IsRejected()
    {
        bool isInitiator = string.CompareOrdinal("device-a", "device-b") < 0;
        var senderHash = SecureInputPacket.ComputeSenderIdHash("device-a");

        using var udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // First set session 5000
        using (var cryptoCurrent = new SessionCrypto(_pairKey, isInitiator, 5000))
        {
            byte[] pt = new byte[14];
            InputMotionPacket.Create(InputMotionPacket.TypeMotion, 1, dx: 5, dy: 5, buttons: 0).TryWrite(pt);
            var ct = cryptoCurrent.Seal(pt, out ulong c);
            udp.SendTo(SecureInputPacket.Write(senderHash, 5000, c, ct), EndPointB);
        }

        await Wait.UntilAsync(() => _sinkB.Snapshot().Length == 1, timeoutMs: 3000);

        long rejectedBefore = _hostB.RejectedPackets;

        // An older session 4000 arrives: must be rejected
        using (var cryptoOld = new SessionCrypto(_pairKey, isInitiator, 4000))
        {
            byte[] ptOld = new byte[14];
            InputMotionPacket.Create(InputMotionPacket.TypeMotion, 1, dx: 99, dy: 99, buttons: 0).TryWrite(ptOld);
            var ctOld = cryptoOld.Seal(ptOld, out ulong cOld);
            udp.SendTo(SecureInputPacket.Write(senderHash, 4000, cOld, ctOld), EndPointB);
        }

        await Task.Delay(100);

        Assert.Single(_sinkB.Snapshot());
        Assert.True(_hostB.RejectedPackets > rejectedBefore);
    }

    [Fact]
    public async Task UnknownSenderHash_DroppedSilently()
    {
        var unknownHash = SecureInputPacket.ComputeSenderIdHash("unregistered-device");
        var fakeCipher = new byte[30];

        using var udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udp.SendTo(SecureInputPacket.Write(unknownHash, 100, 1, fakeCipher), EndPointB);

        await Task.Delay(100);

        Assert.Empty(_sinkB.Snapshot());
        Assert.True(_hostB.RejectedPackets > 0);
    }

    [Fact]
    public async Task V1Packets_RejectedWhenAllowOlderDevicesFalse()
    {
        // _hostB has AllowOlderDevices = false
        using var udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        byte[] v1Packet = new byte[14];
        InputMotionPacket.Create(InputMotionPacket.TypeMotion, 1, 10, 10, 0).TryWrite(v1Packet);

        udp.SendTo(v1Packet, EndPointB);

        await Task.Delay(100);

        Assert.Empty(_sinkB.Snapshot());
        Assert.True(_hostB.RejectedPackets > 0);
    }

    public void Dispose()
    {
        _hostB.Dispose();
    }
}
