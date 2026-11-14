namespace Mouser.Core.Clipboard;

public sealed record ClipDelivery(string DeviceId, string DeviceName, string Status, string? Error = null);
