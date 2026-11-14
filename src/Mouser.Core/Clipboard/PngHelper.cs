using System;
using System.Buffers.Binary;

namespace Mouser.Core.Clipboard;

/// <summary>
/// Lightweight helper to extract PNG dimensions from raw bytes without imaging libraries.
/// </summary>
public static class PngHelper
{
    public static (int Width, int Height) GetDimensions(ReadOnlySpan<byte> pngBytes)
    {
        if (pngBytes.Length >= 24 &&
            pngBytes[0] == 0x89 && pngBytes[1] == 0x50 && pngBytes[2] == 0x4E && pngBytes[3] == 0x47 &&
            pngBytes[4] == 0x0D && pngBytes[5] == 0x0A && pngBytes[6] == 0x1A && pngBytes[7] == 0x0A)
        {
            int width = BinaryPrimitives.ReadInt32BigEndian(pngBytes.Slice(16, 4));
            int height = BinaryPrimitives.ReadInt32BigEndian(pngBytes.Slice(20, 4));
            return (width, height);
        }
        return (0, 0);
    }
}
