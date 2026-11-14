using System;
using System.Runtime.InteropServices;

namespace Mouser.UI.Services;

public static class ClipboardFormats
{
    private static uint _excludeFormat;
    private static bool _registered;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormatW(string lpszFormat);

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    public static bool IsExcludedByOwner()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            if (!_registered)
            {
                _excludeFormat = RegisterClipboardFormatW("ExcludeClipboardContentFromMonitorProcessing");
                _registered = true;
            }

            if (_excludeFormat != 0 && IsClipboardFormatAvailable(_excludeFormat))
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
