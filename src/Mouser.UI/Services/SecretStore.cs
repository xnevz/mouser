using System;
using System.Runtime.InteropServices;

namespace Mouser.UI.Services;

/// <summary>
/// Encrypts and decrypts secrets at rest using Windows DPAPI (CurrentUser).
/// Off Windows, falls back to base64 with a warning comment.
/// </summary>
public static class SecretStore
{
    private static bool _fallbackWarningLogged;

    public static string Protect(byte[]? secret)
    {
        if (secret == null || secret.Length == 0) return string.Empty;

        if (OperatingSystem.IsWindows())
        {
            byte[]? protectedBytes = Dpapi.Protect(secret);
            if (protectedBytes != null)
            {
                return Convert.ToBase64String(protectedBytes);
            }
        }

        // shortcut: no OS key store off Windows, upgrade with libsecret/Keychain
        if (!_fallbackWarningLogged)
        {
            _fallbackWarningLogged = true;
            Console.WriteLine("[SecretStore] Warning: DPAPI not available off Windows, falling back to base64.");
        }
        return Convert.ToBase64String(secret);
    }

    public static byte[]? Unprotect(string? protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText)) return null;

        byte[] cipherBytes;
        try
        {
            cipherBytes = Convert.FromBase64String(protectedText);
        }
        catch
        {
            return null;
        }

        if (OperatingSystem.IsWindows())
        {
            return Dpapi.Unprotect(cipherBytes);
        }

        // shortcut: no OS key store off Windows, upgrade with libsecret/Keychain
        return cipherBytes;
    }

    private static class Dpapi
    {
        private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct DATA_BLOB
        {
            public int cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CryptProtectData(
            ref DATA_BLOB pDataIn,
            string? szDataDescr,
            IntPtr pOptionalEntropy,
            IntPtr pvReserved,
            IntPtr pPromptStruct,
            int dwFlags,
            ref DATA_BLOB pDataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CryptUnprotectData(
            ref DATA_BLOB pDataIn,
            IntPtr ppszDataDescr,
            IntPtr pOptionalEntropy,
            IntPtr pvReserved,
            IntPtr pPromptStruct,
            int dwFlags,
            ref DATA_BLOB pDataOut);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr hMem);

        public static byte[]? Protect(byte[] plaintext)
        {
            var inBlob = new DATA_BLOB
            {
                cbData = plaintext.Length,
                pbData = Marshal.AllocHGlobal(plaintext.Length)
            };
            var outBlob = new DATA_BLOB();

            try
            {
                Marshal.Copy(plaintext, 0, inBlob.pbData, plaintext.Length);
                if (CryptProtectData(ref inBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref outBlob))
                {
                    byte[] result = new byte[outBlob.cbData];
                    Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
                    return result;
                }
                return null;
            }
            finally
            {
                if (inBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(inBlob.pbData);
                if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
            }
        }

        public static byte[]? Unprotect(byte[] ciphertext)
        {
            var inBlob = new DATA_BLOB
            {
                cbData = ciphertext.Length,
                pbData = Marshal.AllocHGlobal(ciphertext.Length)
            };
            var outBlob = new DATA_BLOB();

            try
            {
                Marshal.Copy(ciphertext, 0, inBlob.pbData, ciphertext.Length);
                if (CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref outBlob))
                {
                    byte[] result = new byte[outBlob.cbData];
                    Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
                    return result;
                }
                return null;
            }
            finally
            {
                if (inBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(inBlob.pbData);
                if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
            }
        }
    }
}
