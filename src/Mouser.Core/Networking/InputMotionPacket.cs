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

    public uint Magic;
    public byte Version;
    public byte PacketType; // 0x01: Motion, 0x02: Button, 0x03: Key, 0x04: Ping
    public ushort SequenceId;
    public short DeltaX;
    public short DeltaY;
    public byte ButtonFlags; // Bit 0: Left, Bit 1: Right, Bit 2: Middle
    public byte Modifiers;   // Bit 0: Cmd, Bit 1: Shift, Bit 2: Ctrl, Bit 3: Alt

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputMotionPacket CreateMotion(ushort sequenceId, short dx, short dy, byte buttons = 0, byte modifiers = 0)
    {
        return new InputMotionPacket
        {
            Magic = ExpectedMagic,
            Version = CurrentVersion,
            PacketType = 0x01,
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
