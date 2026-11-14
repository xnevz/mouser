using System.Collections.Generic;

namespace Mouser.Core.Clipboard;

public sealed record ClipboardSnapshot(string? Text, string? Html, byte[]? ImagePng, IReadOnlyList<string>? Files)
{
    public bool IsEmpty => string.IsNullOrEmpty(Text) && string.IsNullOrEmpty(Html) && ImagePng is not { Length: > 0 } && Files is not { Count: > 0 };
}
