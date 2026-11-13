using System.Collections.Concurrent;
using System.Net;
using Mouser.Core.Input;
using Mouser.Core.Networking;

namespace Mouser.Core.Services;

/// <summary>A paired remote device (phone or another desktop) that recently sent input.</summary>
public sealed class RemoteClient
{
    public required IPAddress Address { get; init; }
    public DateTime LastSeenUtc { get; internal set; }
    public long PacketCount { get; internal set; }
    public string LastEvent { get; internal set; } = "Paired";
}

/// <summary>
/// Receives input packets from paired devices and injects them into the local OS.
/// </summary>
public sealed class InputInjectionService : IDisposable
{
    public const int DefaultPort = 58491;

    /// <summary>Held buttons/keys are released when no input arrives for this long (e.g. phone left Wi-Fi mid-drag).</summary>
    public static readonly TimeSpan StuckInputTimeout = TimeSpan.FromSeconds(3);

    private static readonly MouseButton[] Buttons = { MouseButton.Left, MouseButton.Right, MouseButton.Middle };

    private readonly UdpInputReceiver _receiver;
    private readonly IInputSink _sink;
    private readonly PairingGate _gate;
    private readonly Func<DateTime> _clock;
    private readonly SequenceTracker _sequences = new();
    private readonly ConcurrentDictionary<IPAddress, RemoteClient> _clients = new();
    private readonly object _stateLock = new();
    private readonly HashSet<ushort> _heldKeys = new();
    private byte _buttonState;
    private DateTime _lastInputUtc;
    private Timer? _watchdog;
    private bool _isDisposed;

    public bool IsRunning { get; private set; }
    public int BoundPort => _receiver.BoundPort;
    public bool CanInject => _sink.IsSupported;
    public long MotionEventsProcessed { get; private set; }
    public long ButtonEventsProcessed { get; private set; }
    public long KeyEventsProcessed { get; private set; }
    public long ScrollEventsProcessed { get; private set; }
    public long RejectedPackets { get; private set; }
    public long DroppedOutOfOrderPackets { get; private set; }

    public IReadOnlyList<RemoteClient> Clients => _clients.Values.OrderByDescending(c => c.LastSeenUtc).ToList();

    public event Action<InputMotionPacket, IPEndPoint>? PacketProcessed;

    public InputInjectionService(IInputSink sink, PairingGate gate, int listenPort = 0, Func<DateTime>? clock = null)
    {
        _sink = sink;
        _gate = gate;
        _clock = clock ?? (() => DateTime.UtcNow);
        _receiver = new UdpInputReceiver(listenPort);
        _receiver.MotionReceived += HandlePacket;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (IsRunning) return;

        _receiver.Start();
        _watchdog = new Timer(_ => ReleaseHeldInputIfIdle(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _receiver.Stop();
        _watchdog?.Dispose();
        _watchdog = null;
        ReleaseAllHeldInput();
        IsRunning = false;
    }

    public void ForgetClient(IPAddress address)
    {
        _gate.Revoke(address);
        _clients.TryRemove(Normalize(address), out _);
    }

    public void HandlePacket(InputMotionPacket packet, IPEndPoint remoteEndPoint)
    {
        var address = Normalize(remoteEndPoint.Address);

        switch (packet.PacketType)
        {
            case InputMotionPacket.TypePing:
            {
                bool trusted = _gate.IsTrusted(address);
                _receiver.SendTo(InputMotionPacket.Create(InputMotionPacket.TypePong, packet.SequenceId,
                    packet.DeltaX, packet.DeltaY, (byte)(trusted ? 1 : 0)), remoteEndPoint);
                if (trusted) Touch(address, null);
                return;
            }

            case InputMotionPacket.TypePair:
            {
                bool accepted = _gate.TryPair(address, packet.PinValue);
                if (accepted)
                {
                    _sequences.Reset(remoteEndPoint);
                    Touch(address, "Paired");
                }
                _receiver.SendTo(InputMotionPacket.Create(InputMotionPacket.TypePairAck, packet.SequenceId,
                    buttons: (byte)(accepted ? 1 : 0)), remoteEndPoint);
                return;
            }
        }

        if (!_gate.IsTrusted(address))
        {
            RejectedPackets++;
            return;
        }

        if (!_sequences.Accept(remoteEndPoint, packet.SequenceId))
        {
            DroppedOutOfOrderPackets++;
            return;
        }

        string? description;
        lock (_stateLock)
        {
            switch (packet.PacketType)
            {
                case InputMotionPacket.TypeMotion:
                    MotionEventsProcessed++;
                    _sink.MoveRelative(packet.DeltaX, packet.DeltaY);
                    description = ApplyButtonState(packet.ButtonFlags);
                    break;

                case InputMotionPacket.TypeButton:
                    ButtonEventsProcessed++;
                    description = ApplyButtonState(packet.ButtonFlags);
                    break;

                case InputMotionPacket.TypeKey:
                {
                    KeyEventsProcessed++;
                    ushort vk = (ushort)packet.DeltaX;
                    bool isDown = packet.DeltaY != 0;
                    if (isDown) _heldKeys.Add(vk);
                    else _heldKeys.Remove(vk);
                    _sink.Key(vk, isDown);
                    description = isDown ? $"Key 0x{vk:X2}" : null;
                    break;
                }

                case InputMotionPacket.TypeScroll:
                    ScrollEventsProcessed++;
                    _sink.Scroll(packet.DeltaX, packet.DeltaY);
                    description = "Scroll";
                    break;

                default:
                    return;
            }

            _lastInputUtc = _clock();
        }

        Touch(address, description);
        PacketProcessed?.Invoke(packet, remoteEndPoint);
    }

    /// <summary>Releases buttons and keys a silent client left pressed. Returns true if anything was released.</summary>
    public bool ReleaseHeldInputIfIdle()
    {
        lock (_stateLock)
        {
            if (_buttonState == 0 && _heldKeys.Count == 0) return false;
            if (_clock() - _lastInputUtc < StuckInputTimeout) return false;
            ReleaseAllHeldInput();
            return true;
        }
    }

    private void ReleaseAllHeldInput()
    {
        lock (_stateLock)
        {
            ApplyButtonState(0);
            foreach (var vk in _heldKeys) _sink.Key(vk, false);
            _heldKeys.Clear();
        }
    }

    /// <summary>Packets carry the full button mask; emit press/release only for bits that changed.</summary>
    private string? ApplyButtonState(byte newState)
    {
        newState &= 0x07;
        byte changed = (byte)(newState ^ _buttonState);
        if (changed == 0) return null;

        string? description = null;
        foreach (var button in Buttons)
        {
            if ((changed & (byte)button) == 0) continue;
            bool isDown = (newState & (byte)button) != 0;
            _sink.SetButton(button, isDown);
            if (isDown) description = $"{button} click";
        }

        _buttonState = newState;
        return description;
    }

    private void Touch(IPAddress address, string? lastEvent)
    {
        var client = _clients.GetOrAdd(address, a => new RemoteClient { Address = a });
        client.LastSeenUtc = _clock();
        client.PacketCount++;
        if (lastEvent != null) client.LastEvent = lastEvent;
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        _receiver.MotionReceived -= HandlePacket;
        _receiver.Dispose();
    }
}
