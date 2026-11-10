using System.Runtime.CompilerServices;
using Mouser.Core.Networking;
using Xunit;

namespace Mouser.Tests;

public class InputMotionPacketTests
{
    [Fact]
    public void Packet_Size_IsExactly14Bytes()
    {
        Assert.Equal(14, InputMotionPacket.PacketSize);
        Assert.Equal(14, Unsafe.SizeOf<InputMotionPacket>());
    }

    [Fact]
    public void CreateMotion_SetsExpectedFieldsAndMagic()
    {
        var packet = InputMotionPacket.CreateMotion(42, -15, 30, buttons: 0x01, modifiers: 0x02);

        Assert.Equal(InputMotionPacket.ExpectedMagic, packet.Magic);
        Assert.Equal(InputMotionPacket.CurrentVersion, packet.Version);
        Assert.Equal(0x01, packet.PacketType);
        Assert.Equal(42, packet.SequenceId);
        Assert.Equal(-15, packet.DeltaX);
        Assert.Equal(30, packet.DeltaY);
        Assert.Equal(0x01, packet.ButtonFlags);
        Assert.Equal(0x02, packet.Modifiers);
    }

    [Fact]
    public void Serialization_RoundTrip_MatchesOriginalPacket()
    {
        var original = InputMotionPacket.CreateMotion(1001, -250, 480, buttons: 0x03, modifiers: 0x05);
        Span<byte> buffer = stackalloc byte[InputMotionPacket.PacketSize];

        bool writeSuccess = original.TryWrite(buffer);
        Assert.True(writeSuccess);

        bool readSuccess = InputMotionPacket.TryRead(buffer, out var parsed);
        Assert.True(readSuccess);
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void TryRead_RejectsCorruptedMagic()
    {
        var packet = InputMotionPacket.CreateMotion(1, 10, 20);
        byte[] bytes = new byte[InputMotionPacket.PacketSize];
        packet.TryWrite(bytes);

        // Corrupt magic
        bytes[0] = 0x00;

        bool readSuccess = InputMotionPacket.TryRead(bytes, out _);
        Assert.False(readSuccess);
    }

    [Fact]
    public void TryRead_RejectsTruncatedBuffer()
    {
        byte[] truncated = new byte[8];
        bool readSuccess = InputMotionPacket.TryRead(truncated, out _);
        Assert.False(readSuccess);
    }
}
