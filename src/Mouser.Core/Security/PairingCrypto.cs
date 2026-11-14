using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Mouser.Core.Security;

public sealed record PairingResult(byte[] PairKey, string ConfirmationCode);

public sealed class PairingHandshake : IDisposable
{
    private readonly ECDiffieHellman _ecdh;
    private bool _disposed;

    public byte[] PublicKey { get; }

    private PairingHandshake(ECDiffieHellman ecdh, byte[] publicKey)
    {
        _ecdh = ecdh;
        PublicKey = publicKey;
    }

    public static PairingHandshake Start()
    {
        var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdh.ExportParameters(false);
        if (parameters.Q.X == null || parameters.Q.Y == null)
            throw new CryptographicException("Failed to export ephemeral public key coordinates.");

        byte[] raw = new byte[65];
        raw[0] = 0x04; // uncompressed ANSI X9.62 point format
        Buffer.BlockCopy(PadTo32(parameters.Q.X), 0, raw, 1, 32);
        Buffer.BlockCopy(PadTo32(parameters.Q.Y), 0, raw, 33, 32);

        return new PairingHandshake(ecdh, raw);
    }

    public PairingResult Complete(byte[] peerPublicKey, string ownDeviceId, string peerDeviceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (peerPublicKey == null || peerPublicKey.Length != 65 || peerPublicKey[0] != 0x04)
        {
            throw new CryptographicException("Invalid peer public key format; expected 65-byte uncompressed point starting with 0x04.");
        }

        byte[] x = new byte[32];
        byte[] y = new byte[32];
        Buffer.BlockCopy(peerPublicKey, 1, x, 0, 32);
        Buffer.BlockCopy(peerPublicKey, 33, y, 0, 32);

        var peerParams = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = x, Y = y }
        };

        byte[] shared;
        try
        {
            using var peerEcdh = ECDiffieHellman.Create(peerParams);
            shared = _ecdh.DeriveRawSecretAgreement(peerEcdh.PublicKey);
        }
        catch (Exception ex) when (ex is not CryptographicException)
        {
            throw new CryptographicException("Failed to derive shared secret from peer public key.", ex);
        }

        string minId = string.CompareOrdinal(ownDeviceId, peerDeviceId) < 0 ? ownDeviceId : peerDeviceId;
        string maxId = string.CompareOrdinal(ownDeviceId, peerDeviceId) < 0 ? peerDeviceId : ownDeviceId;
        byte[] salt = Encoding.UTF8.GetBytes($"{minId}:{maxId}");

        byte[] prk = HKDF.Extract(HashAlgorithmName.SHA256, shared, salt);
        byte[] okm = HKDF.Expand(HashAlgorithmName.SHA256, prk, 64, Encoding.UTF8.GetBytes("mouser-pair-v2"));
        byte[] pairKey = okm[..32];

        uint codeNumber = BinaryPrimitives.ReadUInt32BigEndian(okm.AsSpan(32, 4)) % 1_000_000;
        string confirmationCode = $"{codeNumber / 1000:D3} {codeNumber % 1000:D3}";

        return new PairingResult(pairKey, confirmationCode);
    }

    private static byte[] PadTo32(byte[] coordinate)
    {
        if (coordinate.Length == 32) return coordinate;
        byte[] padded = new byte[32];
        if (coordinate.Length > 32)
        {
            Buffer.BlockCopy(coordinate, coordinate.Length - 32, padded, 0, 32);
        }
        else
        {
            Buffer.BlockCopy(coordinate, 0, padded, 32 - coordinate.Length, coordinate.Length);
        }
        return padded;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _ecdh.Dispose();
        }
    }
}
