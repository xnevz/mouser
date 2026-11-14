using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Mouser.Core.Clipboard;

namespace Mouser.UI.Services;

/// <summary>
/// Direct Win32 clipboard access for formats not natively surfaced by Avalonia 12.1.6
/// (such as registered "HTML Format", registered "PNG", and direct CF_HDROP / CF_UNICODETEXT).
/// </summary>
public static class Win32Clipboard
{
    private const uint CF_TEXT = 1;
    private const uint CF_BITMAP = 2;
    private const uint CF_DIB = 8;
    private const uint CF_UNICODETEXT = 13;
    private const uint CF_HDROP = 15;
    private const uint CF_DIBV5 = 17;

    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint GMEM_ZEROINIT = 0x0040;
    private const uint GHND = GMEM_MOVEABLE | GMEM_ZEROINIT;

    private static uint _cfHtml;
    private static uint _cfPng;

    public static uint HtmlFormat => _cfHtml != 0 ? _cfHtml : (_cfHtml = RegisterClipboardFormatW("HTML Format"));
    public static uint PngFormat => _cfPng != 0 ? _cfPng : (_cfPng = RegisterClipboardFormatW("PNG"));

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormatW(string lpszFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nuint GlobalSize(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFileW(IntPtr hDrop, uint iFile, [Out] StringBuilder? lpszFile, uint cch);

    [StructLayout(LayoutKind.Sequential)]
    private struct DROPFILES
    {
        public uint pFiles;
        public int ptX;
        public int ptY;
        public bool fNC;
        public bool fWide;
    }

    public static bool Open(int retryCount = 5, int delayMs = 20)
    {
        if (!OperatingSystem.IsWindows()) return false;
        for (int i = 0; i < retryCount; i++)
        {
            if (OpenClipboard(IntPtr.Zero)) return true;
            if (delayMs > 0) Thread.Sleep(delayMs);
        }
        return false;
    }

    public static void Close()
    {
        if (OperatingSystem.IsWindows())
            CloseClipboard();
    }

    public static ClipboardSnapshot GetSnapshot()
    {
        if (!OperatingSystem.IsWindows())
            return new ClipboardSnapshot(null, null, null, null);

        if (!Open())
            return new ClipboardSnapshot(null, null, null, null);

        try
        {
            string? text = ReadUnicodeText();
            string? html = ReadHtml();
            byte[]? png = ReadPng();
            IReadOnlyList<string>? files = ReadFiles();

            return new ClipboardSnapshot(text, html, png, files);
        }
        catch
        {
            return new ClipboardSnapshot(null, null, null, null);
        }
        finally
        {
            Close();
        }
    }

    public static bool SetSnapshot(ClipboardSnapshot s)
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (!Open()) return false;

        try
        {
            EmptyClipboard();

            if (s.Text != null)
            {
                WriteUnicodeText(s.Text);
            }

            if (s.Html != null)
            {
                WriteHtml(s.Html);
            }

            if (s.ImagePng is { Length: > 0 })
            {
                WritePng(s.ImagePng);
            }

            if (s.Files is { Count: > 0 })
            {
                WriteFiles(s.Files);
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Close();
        }
    }

    public static string? ReadUnicodeText()
    {
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return null;
        IntPtr hMem = GetClipboardData(CF_UNICODETEXT);
        if (hMem == IntPtr.Zero) return null;

        IntPtr ptr = GlobalLock(hMem);
        if (ptr == IntPtr.Zero) return null;
        try
        {
            return Marshal.PtrToStringUni(ptr);
        }
        finally
        {
            GlobalUnlock(hMem);
        }
    }

    public static string? ReadHtml()
    {
        uint cf = HtmlFormat;
        if (cf == 0 || !IsClipboardFormatAvailable(cf)) return null;
        IntPtr hMem = GetClipboardData(cf);
        if (hMem == IntPtr.Zero) return null;

        IntPtr ptr = GlobalLock(hMem);
        if (ptr == IntPtr.Zero) return null;
        try
        {
            nuint size = GlobalSize(hMem);
            if (size == 0) return null;

            byte[] buffer = new byte[size];
            Marshal.Copy(ptr, buffer, 0, (int)size);
            int nullIdx = Array.IndexOf(buffer, (byte)0);
            int count = nullIdx >= 0 ? nullIdx : (int)size;
            return Encoding.UTF8.GetString(buffer, 0, count);
        }
        finally
        {
            GlobalUnlock(hMem);
        }
    }

    public static byte[]? ReadPng()
    {
        uint cf = PngFormat;
        if (cf == 0 || !IsClipboardFormatAvailable(cf)) return null;
        IntPtr hMem = GetClipboardData(cf);
        if (hMem == IntPtr.Zero) return null;

        IntPtr ptr = GlobalLock(hMem);
        if (ptr == IntPtr.Zero) return null;
        try
        {
            nuint size = GlobalSize(hMem);
            if (size == 0) return null;

            byte[] buffer = new byte[size];
            Marshal.Copy(ptr, buffer, 0, (int)size);
            return buffer;
        }
        finally
        {
            GlobalUnlock(hMem);
        }
    }

    public static IReadOnlyList<string>? ReadFiles()
    {
        if (!IsClipboardFormatAvailable(CF_HDROP)) return null;
        IntPtr hDrop = GetClipboardData(CF_HDROP);
        if (hDrop == IntPtr.Zero) return null;

        uint count = DragQueryFileW(hDrop, 0xFFFFFFFF, null, 0);
        if (count == 0) return null;

        var list = new List<string>((int)count);
        for (uint i = 0; i < count; i++)
        {
            uint len = DragQueryFileW(hDrop, i, null, 0);
            if (len > 0)
            {
                var sb = new StringBuilder((int)len + 1);
                DragQueryFileW(hDrop, i, sb, (uint)sb.Capacity);
                list.Add(sb.ToString());
            }
        }
        return list;
    }

    public static bool HasDib()
    {
        return IsClipboardFormatAvailable(CF_DIB) || IsClipboardFormatAvailable(CF_DIBV5);
    }

    public static bool HasPng()
    {
        uint cf = PngFormat;
        return cf != 0 && IsClipboardFormatAvailable(cf);
    }

    public static bool HasHtml()
    {
        uint cf = HtmlFormat;
        return cf != 0 && IsClipboardFormatAvailable(cf);
    }

    public static bool HasFiles()
    {
        return IsClipboardFormatAvailable(CF_HDROP);
    }

    private static void WriteUnicodeText(string text)
    {
        int bytesNeeded = (text.Length + 1) * 2;
        IntPtr hMem = GlobalAlloc(GHND, (nuint)bytesNeeded);
        if (hMem == IntPtr.Zero) return;

        IntPtr ptr = GlobalLock(hMem);
        if (ptr == IntPtr.Zero)
        {
            GlobalFree(hMem);
            return;
        }

        try
        {
            Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
            Marshal.WriteInt16(ptr, text.Length * 2, 0); // null terminator
        }
        finally
        {
            GlobalUnlock(hMem);
        }

        if (SetClipboardData(CF_UNICODETEXT, hMem) == IntPtr.Zero)
            GlobalFree(hMem);
    }

    public static string FormatHtmlForClipboard(string html)
    {
        if (html.StartsWith("Version:0.9", StringComparison.OrdinalIgnoreCase) ||
            html.StartsWith("Version:1.0", StringComparison.OrdinalIgnoreCase))
            return html;

        const string headerTemplate =
            "Version:0.9\r\n" +
            "StartHTML:{0:D10}\r\n" +
            "EndHTML:{1:D10}\r\n" +
            "StartFragment:{2:D10}\r\n" +
            "EndFragment:{3:D10}\r\n";

        const string htmlPrefix = "<html><body><!--StartFragment-->";
        const string htmlSuffix = "<!--EndFragment--></body></html>";

        int headerLen = Encoding.UTF8.GetByteCount(string.Format(headerTemplate, 0, 0, 0, 0));
        int prefixLen = Encoding.UTF8.GetByteCount(htmlPrefix);
        int contentLen = Encoding.UTF8.GetByteCount(html);
        int suffixLen = Encoding.UTF8.GetByteCount(htmlSuffix);

        int startHtml = headerLen;
        int startFragment = headerLen + prefixLen;
        int endFragment = startFragment + contentLen;
        int endHtml = endFragment + suffixLen;

        string header = string.Format(headerTemplate, startHtml, endHtml, startFragment, endFragment);
        return header + htmlPrefix + html + htmlSuffix;
    }

    private static void WriteHtml(string html)
    {
        uint cf = HtmlFormat;
        if (cf == 0) return;

        string formatted = FormatHtmlForClipboard(html);
        byte[] bytes = Encoding.UTF8.GetBytes(formatted);
        IntPtr hMem = GlobalAlloc(GHND, (nuint)bytes.Length + 1);
        if (hMem == IntPtr.Zero) return;

        IntPtr ptr = GlobalLock(hMem);
        if (ptr == IntPtr.Zero)
        {
            GlobalFree(hMem);
            return;
        }

        try
        {
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            Marshal.WriteByte(ptr, bytes.Length, 0); // null terminator
        }
        finally
        {
            GlobalUnlock(hMem);
        }

        if (SetClipboardData(cf, hMem) == IntPtr.Zero)
            GlobalFree(hMem);
    }

    private static void WritePng(byte[] pngBytes)
    {
        uint cf = PngFormat;
        if (cf == 0) return;

        IntPtr hMem = GlobalAlloc(GHND, (nuint)pngBytes.Length);
        if (hMem == IntPtr.Zero) return;

        IntPtr ptr = GlobalLock(hMem);
        if (ptr == IntPtr.Zero)
        {
            GlobalFree(hMem);
            return;
        }

        try
        {
            Marshal.Copy(pngBytes, 0, ptr, pngBytes.Length);
        }
        finally
        {
            GlobalUnlock(hMem);
        }

        if (SetClipboardData(cf, hMem) == IntPtr.Zero)
            GlobalFree(hMem);
    }

    private static void WriteFiles(IReadOnlyList<string> files)
    {
        var dropFiles = new DROPFILES
        {
            pFiles = (uint)Marshal.SizeOf<DROPFILES>(),
            ptX = 0,
            ptY = 0,
            fNC = false,
            fWide = true
        };

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        byte[] structBytes = new byte[Marshal.SizeOf<DROPFILES>()];
        IntPtr pStruct = Marshal.AllocHGlobal(structBytes.Length);
        try
        {
            Marshal.StructureToPtr(dropFiles, pStruct, false);
            Marshal.Copy(pStruct, structBytes, 0, structBytes.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(pStruct);
        }

        bw.Write(structBytes);

        foreach (var file in files)
        {
            byte[] fileBytes = Encoding.Unicode.GetBytes(file);
            bw.Write(fileBytes);
            bw.Write((short)0); // null char
        }
        bw.Write((short)0); // trailing null char

        byte[] allBytes = ms.ToArray();
        IntPtr hMem = GlobalAlloc(GHND, (nuint)allBytes.Length);
        if (hMem == IntPtr.Zero) return;

        IntPtr ptr = GlobalLock(hMem);
        if (ptr == IntPtr.Zero)
        {
            GlobalFree(hMem);
            return;
        }

        try
        {
            Marshal.Copy(allBytes, 0, ptr, allBytes.Length);
        }
        finally
        {
            GlobalUnlock(hMem);
        }

        if (SetClipboardData(CF_HDROP, hMem) == IntPtr.Zero)
            GlobalFree(hMem);
    }
}
