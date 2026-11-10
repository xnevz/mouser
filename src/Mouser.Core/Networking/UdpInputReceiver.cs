using System.Net;
using System.Net.Sockets;

namespace Mouser.Core.Networking;

/// <summary>
/// Ultra-low latency UDP receiver handling 1,000Hz motion datagrams with sequence-number ordering.
/// </summary>
public sealed class UdpInputReceiver : IDisposable
{
    private readonly Socket _socket;
    private readonly byte[] _receiveBuffer = new byte[256];
    private EndPoint _senderEndPoint = new IPEndPoint(IPAddress.Any, 0);
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private ushort _highestSequenceReceived;
    private bool _hasReceivedFirstPacket;
    private bool _isDisposed;

    public int BoundPort => ((IPEndPoint)_socket.LocalEndPoint!).Port;
    public long TotalPacketsReceived { get; private set; }
    public long DroppedOutOfOrderPackets { get; private set; }

    public event Action<InputMotionPacket, IPEndPoint>? MotionReceived;

    public UdpInputReceiver(int bindPort = 0)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            ReceiveBufferSize = 256 * 1024
        };
        _socket.Bind(new IPEndPoint(IPAddress.Any, bindPort));
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_listenTask != null) return; // Already running

        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        var memory = new Memory<byte>(_receiveBuffer);

        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await _socket.ReceiveFromAsync(memory, SocketFlags.None, _senderEndPoint, token);
                if (result.ReceivedBytes >= InputMotionPacket.PacketSize)
                {
                    ProcessReceivedData(result.ReceivedBytes, (IPEndPoint)result.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                if (token.IsCancellationRequested) break;
            }
            catch (Exception)
            {
                if (token.IsCancellationRequested) break;
            }
        }
    }

    private void ProcessReceivedData(int receivedBytes, IPEndPoint remoteEndPoint)
    {
        ReadOnlySpan<byte> packetSpan = _receiveBuffer.AsSpan(0, receivedBytes);
        if (InputMotionPacket.TryRead(packetSpan, out var packet))
        {
            TotalPacketsReceived++;

            if (IsSequencedPacketAcceptable(packet.SequenceId))
            {
                _highestSequenceReceived = packet.SequenceId;
                _hasReceivedFirstPacket = true;
                MotionReceived?.Invoke(packet, remoteEndPoint);
            }
            else
            {
                DroppedOutOfOrderPackets++;
            }
        }
    }

    /// <summary>
    /// Checks whether an incoming sequence ID is newer than the highest received, handling 16-bit integer rollover.
    /// </summary>
    public bool IsSequencedPacketAcceptable(ushort incomingSequence)
    {
        if (!_hasReceivedFirstPacket) return true;

        unchecked
        {
            short diff = (short)(incomingSequence - _highestSequenceReceived);
            // Positive diff means newer packet; large negative difference indicates old/delayed packet.
            return diff > 0;
        }
    }

    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try { _listenTask?.Wait(200); } catch { }
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
