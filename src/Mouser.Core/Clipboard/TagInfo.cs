namespace Mouser.Core.Clipboard;

/// <summary>Represents tag metadata, whether system-derived or custom.</summary>
public sealed record TagInfo(string Id, string Name, string Color, int Count, bool IsSystem);
