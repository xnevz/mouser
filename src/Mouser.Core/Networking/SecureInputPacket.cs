using System;
using System.Buffers.Binary;
using System.Text;

namespace Mouser.Core.Networking;

/// <summary>
/// 51-byte authenticated UDP datagram for low-latency encrypted mouse and keyboard input (v2).
/// Layout:
/// - Byte 0: Version (0x02)
/// - Bytes 1..4: SenderIdHash (4 bytes, FNV-1a 32-bit hash of DeviceId)
/// - Bytes 5..12: SessionId (8 bytes, big-endian)
/// - Bytes 13..20: PacketCounter (8 bytes, big-endian)
/// - Bytes 21..34: Encrypted Payload (14 bytes, AES-256-GCM ciphertext of InputMotionPacket)
/// - Bytes 35..50: AEAD Tag (16 bytes)
/// Total size: exactly 51 bytes.
/// </summary>
public static class SecureInputPacket
{
    public const int PacketSize = 51;
    public const byte Version = 2;
    public const int SenderIdHashSize = 4;
    public const int CipherAndTagSize = 30; // 14 bytes ciphertext + 16 bytes tag
    public const int HeaderSize = 21; // 1 + 4 + 8 + 8

    public static uint HashDeviceId(string deviceId)
    {
        uint hash = 2166136261u;
        foreach (byte b in Encoding.UTF8.GetBytes(deviceId))
        {
            hash ^= b;
            hash *= 16777619u;
        }
        return hash;
    }

    public static byte[] ComputeSenderIdHash(string deviceId)
    {
        var bytes = new byte[SenderIdHashSize];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, HashDeviceId(deviceId));
        return bytes;
    }

    public static byte[] Write(byte[] senderIdHash, ulong sessionId, ulong counter, byte[] cipherAndTag)
    {
        if (senderIdHash == null || senderIdHash.Length != SenderIdHashSize)
            throw new ArgumentException($"SenderIdHash must be exactly {SenderIdHashSize} bytes.", nameof(senderIdHash));
        if (cipherAndTag == null || cipherAndTag.Length != CipherAndTagSize)
            throw new ArgumentException($"CipherAndTag must be exactly {CipherAndTagSize} bytes.", nameof(cipherAndTag));

        var buffer = new byte[PacketSize];
        buffer[0] = Version;
        senderIdHash.CopyTo(buffer.AsSpan(1, 4));
        BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(5, 8), sessionId);
        BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(13, 8), counter);
        cipherAndTag.CopyTo(buffer.AsSpan(21, 30));
        return buffer;
    }

    public static void Write(Span<byte> destination, ReadOnlySpan<byte> senderIdHash, ulong sessionId, ulong counter, ReadOnlySpan<byte> cipherAndTag)
    {
        if (destination.Length < PacketSize)
            throw new ArgumentException("Destination too small.", nameof(destination));
        if (senderIdHash.Length != SenderIdHashSize)
            throw new ArgumentException($"SenderIdHash must be exactly {SenderIdHashSize} bytes.", nameof(senderIdHash));
        if (cipherAndTag.Length != CipherAndTagSize)
            throw new ArgumentException($"CipherAndTag must be exactly {CipherAndTagSize} bytes.", nameof(cipherAndTag));

        destination[0] = Version;
        senderIdHash.CopyTo(destination.Slice(1, 4));
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(5, 8), sessionId);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(13, 8), counter);
        cipherAndTag.CopyTo(destination.Slice(21, 30));
    }

    public static bool TryRead(
        ReadOnlySpan<byte> buffer,
        out byte[] senderIdHash,
        out ulong sessionId,
        out ulong counter,
        out byte[] cipherAndTag)
    {
        if (buffer.Length != PacketSize || buffer[0] != Version)
        {
            senderIdHash = Array.Empty<byte>();
            sessionId = 0;
            counter = 0;
            cipherAndTag = Array.Empty<byte>();
            return false;
        }

        senderIdHash = buffer.Slice(1, 4).ToArray();
        sessionId = BinaryPrimitives.ReadUInt64BigEndian(buffer.Slice(5, 8));
        counter = BinaryPrimitives.ReadUInt64BigEndian(buffer.Slice(13, 8));
        cipherAndTag = buffer.Slice(21, 30).ToArray();
        return true;
    }
}
