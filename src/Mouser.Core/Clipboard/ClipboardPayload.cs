using System;
using System.IO;
using System.Text;

namespace Mouser.Core.Clipboard;

/// <summary>
/// Represents a multi-format clipboard payload shared across devices.
/// </summary>
public sealed class ClipboardPayload
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public long TimestampUtc { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public ClipboardContentType ContentType { get; set; } = ClipboardContentType.Text;
    public string TextContent { get; set; } = string.Empty;
    public string? HtmlContent { get; set; }
    public string SourceDevice { get; set; } = Environment.MachineName;
    public bool IsFavorite { get; set; }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        writer.Write(Id);
        writer.Write(TimestampUtc);
        writer.Write((int)ContentType);
        writer.Write(TextContent ?? string.Empty);
        writer.Write(HtmlContent ?? string.Empty);
        writer.Write(SourceDevice ?? string.Empty);
        writer.Write(IsFavorite);

        writer.Flush();
        return ms.ToArray();
    }

    public static ClipboardPayload Deserialize(ReadOnlySpan<byte> bytes)
    {
        using var ms = new MemoryStream(bytes.ToArray());
        using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        return new ClipboardPayload
        {
            Id = reader.ReadString(),
            TimestampUtc = reader.ReadInt64(),
            ContentType = (ClipboardContentType)reader.ReadInt32(),
            TextContent = reader.ReadString(),
            HtmlContent = reader.ReadString(),
            SourceDevice = reader.ReadString(),
            IsFavorite = reader.ReadBoolean()
        };
    }
}
