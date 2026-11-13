using System.Net;
using System.Net.Sockets;

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
    private TaskCompletionSource<bool>? _pairResult;
    private int _sequence; // only the low 16 bits go on the wire
    private long _lastPongTicks;
    private volatile bool _paired;
    private bool _disposed;

    public IPEndPoint Target { get; }
    public bool IsPaired => _paired;
    public bool IsAlive => _paired && _clock() - new DateTime(Interlocked.Read(ref _lastPongTicks), DateTimeKind.Utc) < PongTimeout;
    public ushort CurrentSequence => unchecked((ushort)Volatile.Read(ref _sequence));

    /// <summary>Raised on the receive thread when the peer reports it no longer trusts us (restart or new PIN).</summary>
    public event Action? PairingLost;

    public PeerLink(IPEndPoint target, Func<DateTime>? clock = null)
    {
        Target = target;
        _clock = clock ?? (() => DateTime.UtcNow);
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { SendBufferSize = 128 * 1024 };
        if (OperatingSystem.IsWindows()) _socket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null);
        _socket.Connect(target);

        _ = Task.Run(ReceiveLoopAsync);
        _pingTimer = new Timer(_ => Send(InputMotionPacket.TypePing), null, TimeSpan.Zero, PingInterval);
    }

    /// <summary>Sends the peer's PIN and waits for its answer. False on a wrong PIN, lockout or timeout.</summary>
    public async Task<bool> PairAsync(int pin, TimeSpan? timeout = null)
    {
        var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pairResult = result;
        SendPacket(InputMotionPacket.CreatePair(NextSequence(), pin));
        var finished = await Task.WhenAny(result.Task, Task.Delay(timeout ?? TimeSpan.FromSeconds(2)));
        return finished == result.Task && result.Task.Result;
    }

    public void Send(byte type, short dx = 0, short dy = 0, byte buttons = 0) =>
        SendPacket(InputMotionPacket.Create(type, NextSequence(), dx, dy, buttons));

    // Lock-free: this runs on the input hook thread, which must never block or allocate.
    private ushort NextSequence() => unchecked((ushort)Interlocked.Increment(ref _sequence));

    private void SendPacket(in InputMotionPacket packet)
    {
        if (_disposed) return;
        Span<byte> buffer = stackalloc byte[InputMotionPacket.PacketSize];
        packet.TryWrite(buffer);
        try
        {
            _socket.Send(buffer, SocketFlags.None);
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
        var buffer = new byte[64];
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

            if (!InputMotionPacket.TryRead(buffer.AsSpan(0, received), out var packet)) continue;

            switch (packet.PacketType)
            {
                case InputMotionPacket.TypePong:
                    Interlocked.Exchange(ref _lastPongTicks, _clock().Ticks);
                    if (packet.ButtonFlags == 0 && _paired)
                    {
                        _paired = false;
                        PairingLost?.Invoke();
                    }
                    break;

                case InputMotionPacket.TypePairAck:
                    _paired = packet.ButtonFlags == 1;
                    if (_paired) Interlocked.Exchange(ref _lastPongTicks, _clock().Ticks);
                    _pairResult?.TrySetResult(_paired);
                    break;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pingTimer.Dispose();
        _cts.Cancel();
        _socket.Dispose();
    }
}
