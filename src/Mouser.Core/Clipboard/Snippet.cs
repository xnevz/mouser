using System;

namespace Mouser.Core.Clipboard;

public sealed class Snippet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public long CreatedUtc { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
