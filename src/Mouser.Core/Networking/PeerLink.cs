using System.Net;
using System.Net.Sockets;
using Mouser.Core.Security;

namespace Mouser.Core.Networking;

/// <summary>
/// Outgoing input connection to another Mouser computer: PIN pairing, a ping/pong heartbeat and
/// allocation-free packet sends (safe to call from an input hook at 1 kHz).
/// </summary>
public sealed class PeerLink : IDisposable
{
    public static readonly TimeSpan PingInterval = TimeSpan.FromMilliseconds(500);
    /// <summary>No pong for this long means the peer is gone (sleep, crash, Wi-Fi drop).</summary>
    public static readonly TimeSpan PongTimeout = TimeSpan.FromSeconds(2);

    private const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);

    private readonly Socket _socket;
    private readonly CancellationTokenSource _cts = new();
    private readonly Func<DateTime> _clock;
    private readonly Timer _pingTimer;
    private readonly object _sendLock = new();

    private SessionCrypto? _sessionCrypto;
    private byte[]? _senderIdHash;
    private ulong _sessionId;
    private volatile bool _isSecure;
    private bool _allowOlderDevices = true;

    private int _sequence; // only the low 16 bits go on the wire
    private long _lastPongTicks;
    private volatile bool _paired;
    private bool _disposed;

    public IPEndPoint Target { get; }
    public bool IsPaired => _paired;
    public bool IsAlive => _paired && _clock() - new DateTime(Interlocked.Read(ref _lastPongTicks), DateTimeKind.Utc) < PongTimeout;
    public ushort CurrentSequence => unchecked((ushort)Volatile.Read(ref _sequence));
    public bool IsSecure => _isSecure;

    public bool AllowOlderDevices
    {
        get => _allowOlderDevices;
        set => _allowOlderDevices = value;
    }

    public PeerLink(IPEndPoint target, Func<DateTime>? clock = null, byte[]? pairKey = null, string? ownDeviceId = null, string? peerDeviceId = null, bool allowOlderDevices = true)
    {
        Target = target;
        _clock = clock ?? (() => DateTime.UtcNow);
        _allowOlderDevices = allowOlderDevices;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { SendBufferSize = 128 * 1024 };
        if (OperatingSystem.IsWindows()) _socket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null);
        _socket.Connect(target);

        if (pairKey != null && ownDeviceId != null && peerDeviceId != null)
        {
            SetSecurity(pairKey, ownDeviceId, peerDeviceId);
        }

        _ = Task.Run(ReceiveLoopAsync);
        _pingTimer = new Timer(_ => Send(InputMotionPacket.TypePing), null, TimeSpan.Zero, PingInterval);
    }

    public void SetSecurity(byte[] pairKey, string ownDeviceId, string peerDeviceId, ulong? sessionId = null)
    {
        lock (_sendLock)
        {
            _sessionCrypto?.Dispose();
            _senderIdHash = SecureInputPacket.ComputeSenderIdHash(ownDeviceId);
            _sessionId = sessionId ?? (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            bool isInitiator = string.CompareOrdinal(ownDeviceId, peerDeviceId) < 0;
            _sessionCrypto = new SessionCrypto(pairKey, isInitiator, _sessionId);
            _isSecure = true;
            _paired = true;
            Interlocked.Exchange(ref _lastPongTicks, _clock().Ticks);
        }
    }

    public void Send(byte type, short dx = 0, short dy = 0, byte buttons = 0) =>
        SendPacket(InputMotionPacket.Create(type, NextSequence(), dx, dy, buttons));

    // Lock-free: this runs on the input hook thread, which must never block or allocate.
    private ushort NextSequence() => unchecked((ushort)Interlocked.Increment(ref _sequence));

    private void SendPacket(in InputMotionPacket packet)
    {
        if (_disposed) return;

        if (_isSecure && _senderIdHash != null)
        {
            Span<byte> plaintext = stackalloc byte[InputMotionPacket.PacketSize];
            packet.TryWrite(plaintext);

            byte[] cipherAndTag;
            ulong counter;
            lock (_sendLock)
            {
                if (_sessionCrypto == null) return;
                cipherAndTag = _sessionCrypto.Seal(plaintext, out counter);
            }

            Span<byte> buffer = stackalloc byte[SecureInputPacket.PacketSize];
            SecureInputPacket.Write(buffer, _senderIdHash, _sessionId, counter, cipherAndTag);
            try
            {
                _socket.Send(buffer, SocketFlags.None);
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            return;
        }

        if (!_allowOlderDevices && !_paired) return;

        Span<byte> v1Buffer = stackalloc byte[InputMotionPacket.PacketSize];
        packet.TryWrite(v1Buffer);
        try
        {
            _socket.Send(v1Buffer, SocketFlags.None);
        }
        catch (SocketException)
        {
            // Network down: the missing pongs will report the peer as unreachable.
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[128];
        while (!_cts.IsCancellationRequested)
        {
            int received;
            try
            {
                received = await _socket.ReceiveAsync(buffer, SocketFlags.None, _cts.Token);
            }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { continue; }

            ProcessInboundBytes(buffer, received);
        }
    }

    private void ProcessInboundBytes(byte[] buffer, int received)
    {
        var slice = buffer.AsSpan(0, received);
        if (slice.Length == SecureInputPacket.PacketSize && slice[0] == SecureInputPacket.Version && _isSecure)
        {
            if (SecureInputPacket.TryRead(slice, out _, out _, out ulong counter, out var cipherAndTag))
            {
                byte[] plaintext;
                lock (_sendLock)
                {
                    if (_sessionCrypto == null || !_sessionCrypto.TryOpen(counter, cipherAndTag, out plaintext))
                    {
                        return;
                    }
                }

                if (InputMotionPacket.TryRead(plaintext, out var securePacket))
                {
                    HandleInboundPacket(securePacket);
                }
            }
            return;
        }

        if (InputMotionPacket.TryRead(slice, out var packet))
        {
            HandleInboundPacket(packet);
        }
    }

    private void HandleInboundPacket(InputMotionPacket packet)
    {
        switch (packet.PacketType)
        {
            case InputMotionPacket.TypePong:
                Interlocked.Exchange(ref _lastPongTicks, _clock().Ticks);
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pingTimer.Dispose();
        _cts.Cancel();
        _socket.Dispose();
        lock (_sendLock)
        {
            _sessionCrypto?.Dispose();
            _sessionCrypto = null;
        }
    }
}
