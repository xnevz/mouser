using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mouser.Core.Networking;

/// <summary>
/// High-frequency binary input motion packet (14 bytes, Pack = 1).
/// Designed for zero-allocation memory marshaling at 1,000Hz polling rates.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct InputMotionPacket : IEquatable<InputMotionPacket>
{
    public const uint ExpectedMagic = 0x53554F4D; // 'MOUS' in little-endian
    public const byte CurrentVersion = 0x01;

    // Packet types. Field usage per type (all other fields zero):
    //  Motion:  DeltaX/DeltaY = relative pointer delta, ButtonFlags = full button state mask
    //  Button:  ButtonFlags = full button state mask (host diffs it to emit press/release)
    //  Key:     DeltaX = Windows virtual-key code, DeltaY = 1 down / 0 up
    //  Ping:    client -> host keep-alive; host answers with Pong echoing SequenceId/DeltaX/DeltaY
    //  Scroll:  DeltaX = horizontal, DeltaY = vertical wheel delta (120 = one notch, positive = right/up)
    //  Pair:    DeltaX/DeltaY = high/low 16 bits of the 6-digit PIN
    //  PairAck: host -> client, ButtonFlags = 1 accepted / 0 rejected
    //  Pong:    host -> client, ButtonFlags = 1 if the sender is paired
    public const byte TypeMotion = 0x01;
    public const byte TypeButton = 0x02;
    public const byte TypeKey = 0x03;
    public const byte TypePing = 0x04;
    public const byte TypeScroll = 0x05;
    public const byte TypePair = 0x06;
    public const byte TypePairAck = 0x07;
    public const byte TypePong = 0x08;

    public uint Magic;
    public byte Version;
    public byte PacketType;
    public ushort SequenceId;
    public short DeltaX;
    public short DeltaY;
    public byte ButtonFlags; // Bit 0: Left, Bit 1: Right, Bit 2: Middle
    public byte Modifiers;   // Bit 0: Cmd, Bit 1: Shift, Bit 2: Ctrl, Bit 3: Alt

    public static InputMotionPacket Create(byte type, ushort sequenceId, short dx = 0, short dy = 0, byte buttons = 0) => new()
    {
        Magic = ExpectedMagic,
        Version = CurrentVersion,
        PacketType = type,
        SequenceId = sequenceId,
        DeltaX = dx,
        DeltaY = dy,
        ButtonFlags = buttons
    };

    public static InputMotionPacket CreatePair(ushort sequenceId, int pin) =>
        Create(TypePair, sequenceId, unchecked((short)(pin >> 16)), unchecked((short)(pin & 0xFFFF)));

    public int PinValue => ((ushort)DeltaX << 16) | (ushort)DeltaY;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputMotionPacket CreateMotion(ushort sequenceId, short dx, short dy, byte buttons = 0, byte modifiers = 0)
    {
        return new InputMotionPacket
        {
            Magic = ExpectedMagic,
            Version = CurrentVersion,
            PacketType = TypeMotion,
            SequenceId = sequenceId,
            DeltaX = dx,
            DeltaY = dy,
            ButtonFlags = buttons,
            Modifiers = modifiers
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryRead(ReadOnlySpan<byte> source, out InputMotionPacket packet)
    {
        if (source.Length < Unsafe.SizeOf<InputMotionPacket>())
        {
            packet = default;
            return false;
        }

        packet = MemoryMarshal.Read<InputMotionPacket>(source);
        if (packet.Magic != ExpectedMagic || packet.Version != CurrentVersion)
        {
            packet = default;
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWrite(Span<byte> destination)
    {
        if (destination.Length < Unsafe.SizeOf<InputMotionPacket>())
        {
            return false;
        }

        MemoryMarshal.Write(destination, in this);
        return true;
    }

    public static int PacketSize => Unsafe.SizeOf<InputMotionPacket>();

    public bool Equals(InputMotionPacket other)
    {
        return Magic == other.Magic &&
               Version == other.Version &&
               PacketType == other.PacketType &&
               SequenceId == other.SequenceId &&
               DeltaX == other.DeltaX &&
               DeltaY == other.DeltaY &&
               ButtonFlags == other.ButtonFlags &&
               Modifiers == other.Modifiers;
    }

    public override bool Equals(object? obj) => obj is InputMotionPacket other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Magic, SequenceId, DeltaX, DeltaY, ButtonFlags);
}
