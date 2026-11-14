using System.Buffers.Binary;
using System.Text;

namespace Mouser.Core.Networking;

public sealed record PairRequestPayload(string DeviceId, string DeviceName, byte[] PublicKey)
{
    public byte[] Serialize()
    {
        var idBytes = Encoding.UTF8.GetBytes(DeviceId);
        var nameBytes = Encoding.UTF8.GetBytes(DeviceName);
        var buffer = new byte[2 + idBytes.Length + 2 + nameBytes.Length + 2 + PublicKey.Length];
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteUInt16BigEndian(span[0..2], (ushort)idBytes.Length);
        idBytes.CopyTo(span.Slice(2, idBytes.Length));

        int offset = 2 + idBytes.Length;
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(offset, 2), (ushort)nameBytes.Length);
        offset += 2;
        nameBytes.CopyTo(span.Slice(offset, nameBytes.Length));

        offset += nameBytes.Length;
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(offset, 2), (ushort)PublicKey.Length);
        offset += 2;
        PublicKey.CopyTo(span.Slice(offset, PublicKey.Length));

        return buffer;
    }

    public static PairRequestPayload Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length < 6) throw new InvalidDataException("Payload too short.");
        int idLen = BinaryPrimitives.ReadUInt16BigEndian(data[0..2]);
        if (data.Length < 2 + idLen + 2) throw new InvalidDataException("Invalid id length.");
        string id = Encoding.UTF8.GetString(data.Slice(2, idLen));

        int offset = 2 + idLen;
        int nameLen = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
        offset += 2;
        if (data.Length < offset + nameLen + 2) throw new InvalidDataException("Invalid name length.");
        string name = Encoding.UTF8.GetString(data.Slice(offset, nameLen));

        offset += nameLen;
        int keyLen = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
        offset += 2;
        if (data.Length < offset + keyLen) throw new InvalidDataException("Invalid key length.");
        byte[] key = data.Slice(offset, keyLen).ToArray();

        return new PairRequestPayload(id, name, key);
    }
}

public sealed record PairResponsePayload(string DeviceId, string DeviceName, byte[] PublicKey)
{
    public byte[] Serialize()
    {
        var idBytes = Encoding.UTF8.GetBytes(DeviceId);
        var nameBytes = Encoding.UTF8.GetBytes(DeviceName);
        var buffer = new byte[2 + idBytes.Length + 2 + nameBytes.Length + 2 + PublicKey.Length];
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteUInt16BigEndian(span[0..2], (ushort)idBytes.Length);
        idBytes.CopyTo(span.Slice(2, idBytes.Length));

        int offset = 2 + idBytes.Length;
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(offset, 2), (ushort)nameBytes.Length);
        offset += 2;
        nameBytes.CopyTo(span.Slice(offset, nameBytes.Length));

        offset += nameBytes.Length;
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(offset, 2), (ushort)PublicKey.Length);
        offset += 2;
        PublicKey.CopyTo(span.Slice(offset, PublicKey.Length));

        return buffer;
    }

    public static PairResponsePayload Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length < 6) throw new InvalidDataException("Payload too short.");
        int idLen = BinaryPrimitives.ReadUInt16BigEndian(data[0..2]);
        if (data.Length < 2 + idLen + 2) throw new InvalidDataException("Invalid id length.");
        string id = Encoding.UTF8.GetString(data.Slice(2, idLen));

        int offset = 2 + idLen;
        int nameLen = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
        offset += 2;
        if (data.Length < offset + nameLen + 2) throw new InvalidDataException("Invalid name length.");
        string name = Encoding.UTF8.GetString(data.Slice(offset, nameLen));

        offset += nameLen;
        int keyLen = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
        offset += 2;
        if (data.Length < offset + keyLen) throw new InvalidDataException("Invalid key length.");
        byte[] key = data.Slice(offset, keyLen).ToArray();

        return new PairResponsePayload(id, name, key);
    }
}

public sealed record PairRefusedPayload(string Reason)
{
    public byte[] Serialize() => Encoding.UTF8.GetBytes(Reason);
    public static PairRefusedPayload Deserialize(ReadOnlySpan<byte> data) =>
        new(Encoding.UTF8.GetString(data));
}

public sealed class PairingRequestArgs
{
    public string DeviceName { get; }
    public string DeviceId { get; }
    public string ConfirmationCode { get; }
    public Action Allow { get; }
    public Action Deny { get; }

    public PairingRequestArgs(string deviceName, string deviceId, string confirmationCode, Action allow, Action deny)
    {
        DeviceName = deviceName;
        DeviceId = deviceId;
        ConfirmationCode = confirmationCode;
        Allow = allow;
        Deny = deny;
    }
}

public sealed record PairingInitiatorResult(
    bool IsSuccess,
    bool IsRefused,
    string? Reason,
    string? DeviceId,
    string? DeviceName,
    byte[]? PairKey,
    string? ConfirmationCode)
{
    public static PairingInitiatorResult Success(string deviceId, string deviceName, byte[] pairKey, string confirmationCode) =>
        new(true, false, null, deviceId, deviceName, pairKey, confirmationCode);

    public static PairingInitiatorResult Refused(string reason) =>
        new(false, true, reason, null, null, null, null);

    public static PairingInitiatorResult Failed(string reason) =>
        new(false, false, reason, null, null, null, null);
}
