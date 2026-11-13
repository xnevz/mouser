using System.Net;
using System.Net.Sockets;

namespace Mouser.Core.Networking;

/// <summary>
/// UDP receiver for 14-byte input packets. Validates framing only; ordering and trust are the caller's job.
/// </summary>
public sealed class UdpInputReceiver : IDisposable
{
    private const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);

    private readonly Socket _socket;
    private readonly byte[] _receiveBuffer = new byte[256];
    private readonly EndPoint _anyEndPoint = new IPEndPoint(IPAddress.Any, 0);
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _isDisposed;

    public int BoundPort => ((IPEndPoint)_socket.LocalEndPoint!).Port;
    public long TotalPacketsReceived { get; private set; }

    public event Action<InputMotionPacket, IPEndPoint>? MotionReceived;

    public UdpInputReceiver(int bindPort = 0)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            ReceiveBufferSize = 256 * 1024
        };

        if (OperatingSystem.IsWindows())
        {
            // Without this, an ICMP "port unreachable" from a vanished client makes every later receive throw.
            _socket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null);
        }

        _socket.Bind(new IPEndPoint(IPAddress.Any, bindPort));
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_listenTask != null) return;

        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    /// <summary>Sends a reply packet (pong, pair ack) back to a client.</summary>
    public void SendTo(in InputMotionPacket packet, IPEndPoint target)
    {
        if (_isDisposed) return;
        Span<byte> buffer = stackalloc byte[InputMotionPacket.PacketSize];
        packet.TryWrite(buffer);
        try
        {
            _socket.SendTo(buffer, SocketFlags.None, target);
        }
        catch (SocketException)
        {
            // Client went away; replies are best-effort.
        }
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        var memory = new Memory<byte>(_receiveBuffer);

        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await _socket.ReceiveFromAsync(memory, SocketFlags.None, _anyEndPoint, token);
                if (InputMotionPacket.TryRead(_receiveBuffer.AsSpan(0, result.ReceivedBytes), out var packet))
                {
                    TotalPacketsReceived++;
                    MotionReceived?.Invoke(packet, (IPEndPoint)result.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                // A faulty packet or handler must not kill the listener.
                if (token.IsCancellationRequested) break;
            }
        }
    }

    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try { _listenTask?.Wait(500); } catch { }
            _cts.Dispose();
            _cts = null;
            _listenTask = null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        _socket.Dispose();
    }
}
