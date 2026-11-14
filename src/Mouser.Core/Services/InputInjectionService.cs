using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using Mouser.Core.Input;
using Mouser.Core.Networking;
using Mouser.Core.Security;

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

    private sealed class SecurePeerEntry : IDisposable
    {
        public string DeviceId { get; }
        public byte[] PairKey { get; }
        public object Lock { get; } = new();
        public ulong LastSeenSessionId { get; set; }
        public SessionCrypto? Crypto { get; set; }

        public SecurePeerEntry(string deviceId, byte[] pairKey)
        {
            DeviceId = deviceId;
            PairKey = pairKey;
        }

        public void Dispose()
        {
            lock (Lock)
            {
                Crypto?.Dispose();
                Crypto = null;
            }
        }
    }

    private readonly ConcurrentDictionary<uint, SecurePeerEntry> _securePeers = new();
    private readonly string _ownDeviceId;
    public bool AllowOlderDevices { get; set; } = false;
    public bool LocalNetworkOnly
    {
        get => _receiver.LocalNetworkOnly;
        set => _receiver.LocalNetworkOnly = value;
    }

    private readonly UdpInputReceiver _receiver;
    private readonly IInputSink _sink;
    private readonly Func<DateTime> _clock;
    private readonly SequenceTracker _sequences = new();
    private readonly ConcurrentDictionary<IPAddress, RemoteClient> _clients = new();
    private readonly object _stateLock = new();
    private readonly HashSet<ushort> _heldKeys = new();
    private readonly HashSet<ushort> _heldChars = new();
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
    public long CharEventsProcessed { get; private set; }
    public long ScrollEventsProcessed { get; private set; }
    public long RejectedPackets { get; private set; }
    public long DroppedOutOfOrderPackets { get; private set; }

    public IReadOnlyList<RemoteClient> Clients => _clients.Values.OrderByDescending(c => c.LastSeenUtc).ToList();

    public event Action<InputMotionPacket, IPEndPoint>? PacketProcessed;
    public event Action<string, IPEndPoint>? PeerAuthenticated;
    public event Action<bool>? LaserStateChanged;

    public InputInjectionService(IInputSink sink, int listenPort = 0, Func<DateTime>? clock = null, string? ownDeviceId = null, IPAddress? bindAddress = null)
    {
        _sink = sink;
        _clock = clock ?? (() => DateTime.UtcNow);
        _ownDeviceId = string.IsNullOrWhiteSpace(ownDeviceId) ? Guid.NewGuid().ToString("N") : ownDeviceId;
        _receiver = new UdpInputReceiver(listenPort, bindAddress);
        _receiver.DatagramReceived += HandleDatagram;
    }

    public void RegisterPeer(string deviceId, byte[] pairKey)
    {
        uint hash = SecureInputPacket.HashDeviceId(deviceId);
        _securePeers[hash] = new SecurePeerEntry(deviceId, pairKey);
    }

    public void UnregisterPeer(string deviceId)
    {
        uint hash = SecureInputPacket.HashDeviceId(deviceId);
        if (_securePeers.TryRemove(hash, out var entry))
        {
            entry.Dispose();
        }
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
        _clients.TryRemove(Normalize(address), out _);
    }

    public void HandleDatagram(ReadOnlyMemory<byte> datagram, IPEndPoint remoteEndPoint)
    {
        var span = datagram.Span;
        if (span.Length == SecureInputPacket.PacketSize && span[0] == SecureInputPacket.Version)
        {
            HandleSecureDatagram(span, remoteEndPoint);
            return;
        }

        if (AllowOlderDevices && InputMotionPacket.TryRead(span, out var v1Packet))
        {
            HandlePacket(v1Packet, remoteEndPoint);
            return;
        }

        RejectedPackets++;
    }

    private void HandleSecureDatagram(ReadOnlySpan<byte> datagram, IPEndPoint remoteEndPoint)
    {
        if (!SecureInputPacket.TryRead(datagram, out var senderIdHashBytes, out ulong sessionId, out ulong counter, out var cipherAndTag))
        {
            RejectedPackets++;
            return;
        }

        uint hash = BinaryPrimitives.ReadUInt32BigEndian(senderIdHashBytes);
        if (!_securePeers.TryGetValue(hash, out var peerEntry))
        {
            RejectedPackets++;
            return;
        }

        byte[] plaintext;
        lock (peerEntry.Lock)
        {
            if (peerEntry.Crypto == null || sessionId > peerEntry.LastSeenSessionId)
            {
                peerEntry.Crypto?.Dispose();
                bool isInitiator = string.CompareOrdinal(_ownDeviceId, peerEntry.DeviceId) < 0;
                peerEntry.Crypto = new SessionCrypto(peerEntry.PairKey, isInitiator, sessionId);
                peerEntry.LastSeenSessionId = sessionId;
            }
            else if (sessionId < peerEntry.LastSeenSessionId)
            {
                RejectedPackets++;
                return;
            }

            if (!peerEntry.Crypto.TryOpen(counter, cipherAndTag, out plaintext))
            {
                RejectedPackets++;
                return;
            }
        }

        PeerAuthenticated?.Invoke(peerEntry.DeviceId, remoteEndPoint);

        if (!InputMotionPacket.TryRead(plaintext, out var packet))
        {
            RejectedPackets++;
            return;
        }

        if (packet.PacketType == InputMotionPacket.TypePing)
        {
            Touch(Normalize(remoteEndPoint.Address), null);
            Span<byte> pongPlaintext = stackalloc byte[InputMotionPacket.PacketSize];
            InputMotionPacket.Create(InputMotionPacket.TypePong, packet.SequenceId, packet.DeltaX, packet.DeltaY, 1).TryWrite(pongPlaintext);
            lock (peerEntry.Lock)
            {
                if (peerEntry.Crypto != null)
                {
                    byte[] replyCipher = peerEntry.Crypto.Seal(pongPlaintext, out ulong replyCounter);
                    Span<byte> replyDatagram = stackalloc byte[SecureInputPacket.PacketSize];
                    byte[] ownHash = SecureInputPacket.ComputeSenderIdHash(_ownDeviceId);
                    SecureInputPacket.Write(replyDatagram, ownHash, sessionId, replyCounter, replyCipher);
                    _receiver.SendBytesTo(replyDatagram, remoteEndPoint);
                }
            }
            return;
        }

        ProcessPacketPayload(packet, remoteEndPoint);
    }

    public void HandlePacket(InputMotionPacket packet, IPEndPoint remoteEndPoint)
    {
        if (!AllowOlderDevices)
        {
            RejectedPackets++;
            return;
        }

        var address = Normalize(remoteEndPoint.Address);

        switch (packet.PacketType)
        {
            case InputMotionPacket.TypePing:
            {
                _receiver.SendTo(InputMotionPacket.Create(InputMotionPacket.TypePong, packet.SequenceId,
                    packet.DeltaX, packet.DeltaY, 0), remoteEndPoint);
                return;
            }
        }

        if (!_sequences.Accept(remoteEndPoint, packet.SequenceId))
        {
            DroppedOutOfOrderPackets++;
            return;
        }

        ProcessPacketPayload(packet, remoteEndPoint);
    }

    private void ProcessPacketPayload(InputMotionPacket packet, IPEndPoint remoteEndPoint, string? customEvent = null)
    {
        var address = Normalize(remoteEndPoint.Address);
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

                case InputMotionPacket.TypeMoveAbsolute:
                    MotionEventsProcessed++;
                    _sink.MoveAbsolute(packet.DeltaX, packet.DeltaY);
                    description = null;
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

                case InputMotionPacket.TypeChar:
                {
                    CharEventsProcessed++;
                    ushort unit = (ushort)packet.DeltaX;
                    bool isDown = packet.DeltaY == 1 || (packet.ButtonFlags & 0x01) != 0;
                    if (isDown) _heldChars.Add(unit);
                    else _heldChars.Remove(unit);
                    _sink.Char(unit, isDown);
                    description = isDown ? $"Char 0x{unit:X4}" : null;
                    break;
                }

                case InputMotionPacket.TypeScroll:
                    ScrollEventsProcessed++;
                    _sink.Scroll(packet.DeltaX, packet.DeltaY);
                    description = "Scroll";
                    break;

                case InputMotionPacket.TypeLaser:
                {
                    bool isOn = packet.DeltaY == 1 || (packet.ButtonFlags & 0x01) != 0;
                    LaserStateChanged?.Invoke(isOn);
                    description = isOn ? "Laser ON" : "Laser OFF";
                    break;
                }

                default:
                    return;
            }

            _lastInputUtc = _clock();
        }

        Touch(address, customEvent ?? description);
        PacketProcessed?.Invoke(packet, remoteEndPoint);
    }

    /// <summary>Releases buttons and keys a silent client left pressed. Returns true if anything was released.</summary>
    public bool ReleaseHeldInputIfIdle()
    {
        lock (_stateLock)
        {
            if (_buttonState == 0 && _heldKeys.Count == 0 && _heldChars.Count == 0) return false;
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
            foreach (var unit in _heldChars) _sink.Char(unit, false);
            _heldChars.Clear();
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
        _receiver.DatagramReceived -= HandleDatagram;
        _receiver.Dispose();
        foreach (var entry in _securePeers.Values)
        {
            entry.Dispose();
        }
        _securePeers.Clear();
    }
}
