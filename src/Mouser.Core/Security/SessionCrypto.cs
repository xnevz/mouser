using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Mouser.Core.Security;

public sealed class SessionCrypto : IDisposable
{
    private readonly AesGcm _sendAes;
    private readonly AesGcm _recvAes;
    private readonly object _lock = new();

    private long _sendCounter;
    private ulong _highestReceivedCounter;
    private ulong _window;
    private bool _disposed;

    public SessionCrypto(byte[] pairKey, bool isInitiator, ulong sessionId)
    {
        if (pairKey == null || pairKey.Length != 32)
            throw new ArgumentException("PairKey must be exactly 32 bytes.", nameof(pairKey));

        byte[] sessionSalt = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(sessionSalt, sessionId);

        byte[] prk = HKDF.Extract(HashAlgorithmName.SHA256, pairKey, sessionSalt);
        byte[] kA2B = HKDF.Expand(HashAlgorithmName.SHA256, prk, 32, Encoding.UTF8.GetBytes("mouser-a2b"));
        byte[] kB2A = HKDF.Expand(HashAlgorithmName.SHA256, prk, 32, Encoding.UTF8.GetBytes("mouser-b2a"));

        byte[] sendKey = isInitiator ? kA2B : kB2A;
        byte[] recvKey = isInitiator ? kB2A : kA2B;

        _sendAes = new AesGcm(sendKey, 16);
        _recvAes = new AesGcm(recvKey, 16);
    }

    public byte[] Seal(ReadOnlySpan<byte> plaintext, out ulong counter)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        long c = System.Threading.Interlocked.Increment(ref _sendCounter);
        counter = (ulong)c;

        Span<byte> nonce = stackalloc byte[12];
        nonce[..4].Clear();
        BinaryPrimitives.WriteUInt64BigEndian(nonce[4..], counter);

        byte[] output = new byte[plaintext.Length + 16];
        Span<byte> cipherSpan = output.AsSpan(0, plaintext.Length);
        Span<byte> tagSpan = output.AsSpan(plaintext.Length, 16);

        _sendAes.Encrypt(nonce, plaintext, cipherSpan, tagSpan);
        return output;
    }

    public bool TryOpen(ulong counter, ReadOnlySpan<byte> cipherAndTag, out byte[] plaintext)
    {
        plaintext = Array.Empty<byte>();
        if (_disposed || counter == 0 || cipherAndTag.Length < 16)
        {
            return false;
        }

        lock (_lock)
        {
            if (_highestReceivedCounter > 0 && counter <= _highestReceivedCounter)
            {
                ulong diff = _highestReceivedCounter - counter;
                if (diff >= 64) return false; // outside window
                ulong bit = 1UL << (int)diff;
                if ((_window & bit) != 0) return false; // replayed packet
            }
        }

        Span<byte> nonce = stackalloc byte[12];
        nonce[..4].Clear();
        BinaryPrimitives.WriteUInt64BigEndian(nonce[4..], counter);

        int cipherLen = cipherAndTag.Length - 16;
        ReadOnlySpan<byte> cipher = cipherAndTag[..cipherLen];
        ReadOnlySpan<byte> tag = cipherAndTag[cipherLen..];

        byte[] decrypted = new byte[cipherLen];
        try
        {
            _recvAes.Decrypt(nonce, cipher, tag, decrypted);
        }
        catch (CryptographicException)
        {
            return false;
        }

        lock (_lock)
        {
            if (_highestReceivedCounter == 0)
            {
                _highestReceivedCounter = counter;
                _window = 1UL;
            }
            else if (counter > _highestReceivedCounter)
            {
                ulong diff = counter - _highestReceivedCounter;
                if (diff < 64)
                {
                    _window = (_window << (int)diff) | 1UL;
                }
                else
                {
                    _window = 1UL;
                }
                _highestReceivedCounter = counter;
            }
            else
            {
                ulong diff = _highestReceivedCounter - counter;
                if (diff < 64)
                {
                    _window |= (1UL << (int)diff);
                }
            }
        }

        plaintext = decrypted;
        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _sendAes.Dispose();
            _recvAes.Dispose();
        }
    }
}
