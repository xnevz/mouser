using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Mouser.Core.Networking;

/// <summary>
/// Ultra-low latency UDP transmitter streaming binary motion packets.
/// Allocates zero heap memory during active packet streaming.
/// </summary>
public sealed class UdpInputTransmitter : IDisposable
{
    private readonly Socket _socket;
    private IPEndPoint? _targetEndPoint;
    private ushort _sequenceCounter;
    private bool _isConnected;
    private bool _isDisposed;

    public UdpInputTransmitter()
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            SendBufferSize = 128 * 1024
        };

        if (OperatingSystem.IsWindows())
        {
            try { _socket.DontFragment = true; } catch { }
        }
    }

    public UdpInputTransmitter(IPEndPoint targetEndPoint) : this()
    {
        TargetEndPoint = targetEndPoint;
    }

    public IPEndPoint? TargetEndPoint
    {
        get => _targetEndPoint;
        set
        {
            _targetEndPoint = value;
            if (_targetEndPoint != null)
            {
                _socket.Connect(_targetEndPoint);
                _isConnected = true;
            }
            else
            {
                _isConnected = false;
            }
        }
    }

    public ushort CurrentSequence => _sequenceCounter;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int SendMotion(short dx, short dy, byte buttons = 0, byte modifiers = 0)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!_isConnected)
        {
            if (_targetEndPoint == null)
            {
                throw new InvalidOperationException("TargetEndPoint must be configured before sending packets.");
            }
            _socket.Connect(_targetEndPoint);
            _isConnected = true;
        }

        ushort nextSeq = unchecked(++_sequenceCounter);
        var packet = InputMotionPacket.CreateMotion(nextSeq, dx, dy, buttons, modifiers);

        Span<byte> buffer = stackalloc byte[InputMotionPacket.PacketSize];
        packet.TryWrite(buffer);

        return _socket.Send(buffer, SocketFlags.None);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int SendPacket(in InputMotionPacket packet)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!_isConnected)
        {
            if (_targetEndPoint == null)
            {
                throw new InvalidOperationException("TargetEndPoint must be configured before sending packets.");
            }
            _socket.Connect(_targetEndPoint);
            _isConnected = true;
        }

        Span<byte> buffer = stackalloc byte[InputMotionPacket.PacketSize];
        packet.TryWrite(buffer);

        return _socket.Send(buffer, SocketFlags.None);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _socket.Dispose();
    }
}
