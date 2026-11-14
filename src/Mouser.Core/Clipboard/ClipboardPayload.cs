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
    public string SourceApp { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public string? MediaPath { get; set; }
    public string? MediaHash { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public long? MediaBytes { get; set; }
    public DateTime CreatedTime => DateTimeOffset.FromUnixTimeMilliseconds(TimestampUtc).LocalDateTime;
    public string GetFullText() => ClipboardStorageService.GetFullText(this);

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
        writer.Write(SourceApp ?? string.Empty);
        writer.Write(MediaPath ?? string.Empty);
        writer.Write(MediaHash ?? string.Empty);
        writer.Write(Width ?? 0);
        writer.Write(Height ?? 0);
        writer.Write(MediaBytes ?? 0L);

        writer.Flush();
        return ms.ToArray();
    }

    public static ClipboardPayload Deserialize(ReadOnlySpan<byte> bytes)
    {
        using var ms = new MemoryStream(bytes.ToArray());
        using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        var payload = new ClipboardPayload
        {
            Id = reader.ReadString(),
            TimestampUtc = reader.ReadInt64(),
            ContentType = (ClipboardContentType)reader.ReadInt32(),
            TextContent = reader.ReadString(),
            HtmlContent = reader.ReadString(),
            SourceDevice = reader.ReadString(),
            IsFavorite = reader.ReadBoolean()
        };

        if (ms.Position < ms.Length)
        {
            payload.SourceApp = reader.ReadString();
        }

        if (ms.Position < ms.Length)
        {
            var mp = reader.ReadString();
            payload.MediaPath = string.IsNullOrEmpty(mp) ? null : mp;
        }
        if (ms.Position < ms.Length)
        {
            var mh = reader.ReadString();
            payload.MediaHash = string.IsNullOrEmpty(mh) ? null : mh;
        }
        if (ms.Position < ms.Length)
        {
            var w = reader.ReadInt32();
            payload.Width = w == 0 ? null : w;
        }
        if (ms.Position < ms.Length)
        {
            var h = reader.ReadInt32();
            payload.Height = h == 0 ? null : h;
        }
        if (ms.Position < ms.Length)
        {
            var b = reader.ReadInt64();
            payload.MediaBytes = b == 0 ? null : b;
        }

        return payload;
    }
}
