using System;
using System.Runtime.InteropServices;

namespace Mouser.Core.Networking;

/// <summary>
/// Packed 16-byte binary packet for streaming keyboard events over UDP/TCP streams.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct InputKeyEventPacket
{
    public const uint ExpectedMagic = 0x53554F4D; // 'MOUS' in little-endian
    public const byte ExpectedVersion = 0x01;
    public const byte ExpectedPacketType = 0x02;   // 0x02 = Key Event

    public uint Magic;          // 4 bytes: 'MOUS'
    public byte Version;        // 1 byte:  0x01
    public byte PacketType;     // 1 byte:  0x02 (Key)
    public ushort SequenceId;   // 2 bytes: Monotonically increasing
    public uint KeyCode;        // 4 bytes: Translated Keycode
    public byte Modifiers;      // 1 byte:  Modifier bitmask
    public byte IsDown;         // 1 byte:  1 = KeyDown, 0 = KeyUp
    public ushort Reserved;     // 2 bytes: Padding/Future use (Total: 16 bytes)

    public static InputKeyEventPacket Create(ushort seq, uint keyCode, byte modifiers, bool isDown)
    {
        return new InputKeyEventPacket
        {
            Magic = ExpectedMagic,
            Version = ExpectedVersion,
            PacketType = ExpectedPacketType,
            SequenceId = seq,
            KeyCode = keyCode,
            Modifiers = modifiers,
            IsDown = (byte)(isDown ? 1 : 0),
            Reserved = 0
        };
    }

    public bool IsValid() => Magic == ExpectedMagic && Version == ExpectedVersion && PacketType == ExpectedPacketType;
}
