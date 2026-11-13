namespace Mouser.Core.Clipboard;

/// <summary>
/// Identifies the format and representation type of a clipboard item.
/// </summary>
public enum ClipboardContentType
{
    Text = 1,
    Code = 2,
    Html = 3,
    Rtf = 4,
    Image = 5,
    FileUri = 6,
    Link = 7
}
