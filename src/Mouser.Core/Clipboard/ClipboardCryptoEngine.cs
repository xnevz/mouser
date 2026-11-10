using System;
using System.IO;
using System.Security.Cryptography;

namespace Mouser.Core.Clipboard;

/// <summary>
/// Provides End-to-End Encryption (E2EE) using 256-bit AES-GCM authenticated encryption
/// for clipboard synchronization across devices.
/// </summary>
public static class ClipboardCryptoEngine
{
    public const int KeySizeBytes = 32;   // 256 bits
    public const int NonceSizeBytes = 12; // 96-bit standard GCM nonce
    public const int TagSizeBytes = 16;   // 128-bit authentication tag

    public static byte[] GenerateKey()
    {
        return RandomNumberGenerator.GetBytes(KeySizeBytes);
    }

    /// <summary>
    /// Encrypts plaintext bytes into a binary container: [Nonce (12B)][Tag (16B)][Ciphertext].
    /// </summary>
    public static byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySizeBytes)
        {
            throw new ArgumentException($"Encryption key must be {KeySizeBytes} bytes.", nameof(key));
        }

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        using var ms = new MemoryStream(NonceSizeBytes + TagSizeBytes + ciphertext.Length);
        ms.Write(nonce);
        ms.Write(tag);
        ms.Write(ciphertext);

        return ms.ToArray();
    }

    /// <summary>
    /// Decrypts a container: [Nonce (12B)][Tag (16B)][Ciphertext] and verifies authenticity.
    /// </summary>
    public static byte[] Decrypt(ReadOnlySpan<byte> encryptedContainer, ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySizeBytes)
        {
            throw new ArgumentException($"Decryption key must be {KeySizeBytes} bytes.", nameof(key));
        }

        int minSize = NonceSizeBytes + TagSizeBytes;
        if (encryptedContainer.Length < minSize)
        {
            throw new CryptographicException("Encrypted payload is corrupted or truncated.");
        }

        ReadOnlySpan<byte> nonce = encryptedContainer[..NonceSizeBytes];
        ReadOnlySpan<byte> tag = encryptedContainer.Slice(NonceSizeBytes, TagSizeBytes);
        ReadOnlySpan<byte> ciphertext = encryptedContainer[(NonceSizeBytes + TagSizeBytes)..];

        byte[] plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }
}
