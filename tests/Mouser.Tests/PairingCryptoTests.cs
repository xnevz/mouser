using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mouser.Core.Security;
using Xunit;

namespace Mouser.Tests;

public class PairingCryptoTests
{
    [Fact]
    public void Handshake_DerivesSamePairKeyAndConfirmationCode()
    {
        using var alice = PairingHandshake.Start();
        using var bob = PairingHandshake.Start();

        var aliceResult = alice.Complete(bob.PublicKey, "device-alice", "device-bob");
        var bobResult = bob.Complete(alice.PublicKey, "device-bob", "device-alice");

        Assert.Equal(32, aliceResult.PairKey.Length);
        Assert.Equal(32, bobResult.PairKey.Length);
        Assert.Equal(aliceResult.PairKey, bobResult.PairKey);
        Assert.Equal(aliceResult.ConfirmationCode, bobResult.ConfirmationCode);
    }

    [Fact]
    public void Handshake_ConfirmationCode_HasCorrectFormat()
    {
        using var a = PairingHandshake.Start();
        using var b = PairingHandshake.Start();

        var result = a.Complete(b.PublicKey, "dev-a", "dev-b");

        Assert.Matches(@"^\d{3} \d{3}$", result.ConfirmationCode);
    }

    [Fact]
    public void Handshake_DifferentPeers_DeriveDifferentKeys()
    {
        using var a = PairingHandshake.Start();
        using var b = PairingHandshake.Start();
        using var c = PairingHandshake.Start();

        var resAB = a.Complete(b.PublicKey, "dev-a", "dev-b");
        var resAC = a.Complete(c.PublicKey, "dev-a", "dev-c");

        Assert.NotEqual(resAB.PairKey, resAC.PairKey);
        Assert.NotEqual(resAB.ConfirmationCode, resAC.ConfirmationCode);
    }

    [Fact]
    public void Handshake_DifferentDeviceIds_DeriveDifferentKeys()
    {
        using var a = PairingHandshake.Start();
        using var b = PairingHandshake.Start();

        // Using same public keys but different device id salt
        var res1 = a.Complete(b.PublicKey, "dev-1", "dev-2");
        var res2 = a.Complete(b.PublicKey, "dev-x", "dev-y");

        Assert.NotEqual(res1.PairKey, res2.PairKey);
    }

    [Fact]
    public void Handshake_InvalidPublicKeyLength_ThrowsCryptographicException()
    {
        using var a = PairingHandshake.Start();
        byte[] invalidKey = new byte[64];
        invalidKey[0] = 0x04;

        Assert.Throws<CryptographicException>(() => a.Complete(invalidKey, "dev-a", "dev-b"));
    }

    [Fact]
    public void Handshake_InvalidPublicKeyPrefix_ThrowsCryptographicException()
    {
        using var a = PairingHandshake.Start();
        using var b = PairingHandshake.Start();

        byte[] tamperedKey = (byte[])b.PublicKey.Clone();
        tamperedKey[0] = 0x02; // compressed prefix instead of 0x04

        Assert.Throws<CryptographicException>(() => a.Complete(tamperedKey, "dev-a", "dev-b"));
    }

    [Fact]
    public void SessionCrypto_RoundTrip_SuccessfullyDecryptsPlaintext()
    {
        byte[] pairKey = new byte[32];
        RandomNumberGenerator.Fill(pairKey);
        ulong sessionId = 1700000000123UL;

        using var alice = new SessionCrypto(pairKey, isInitiator: true, sessionId);
        using var bob = new SessionCrypto(pairKey, isInitiator: false, sessionId);

        // Alice -> Bob
        byte[] msg1 = Encoding.UTF8.GetBytes("Hello from Alice!");
        byte[] sealed1 = alice.Seal(msg1, out ulong c1);
        Assert.Equal(1UL, c1);
        bool open1 = bob.TryOpen(c1, sealed1, out byte[] opened1);
        Assert.True(open1);
        Assert.Equal(msg1, opened1);

        // Bob -> Alice
        byte[] msg2 = Encoding.UTF8.GetBytes("Hi Alice, this is Bob.");
        byte[] sealed2 = bob.Seal(msg2, out ulong c2);
        Assert.Equal(1UL, c2);
        bool open2 = alice.TryOpen(c2, sealed2, out byte[] opened2);
        Assert.True(open2);
        Assert.Equal(msg2, opened2);
    }

    [Fact]
    public void SessionCrypto_TamperedCiphertext_Fails()
    {
        byte[] pairKey = new byte[32];
        RandomNumberGenerator.Fill(pairKey);
        using var alice = new SessionCrypto(pairKey, isInitiator: true, 42);
        using var bob = new SessionCrypto(pairKey, isInitiator: false, 42);

        byte[] sealedMsg = alice.Seal("Sensitive input"u8, out ulong c);
        sealedMsg[0] ^= 0xFF; // tamper ciphertext

        bool opened = bob.TryOpen(c, sealedMsg, out byte[] plaintext);
        Assert.False(opened);
        Assert.Empty(plaintext);
    }

    [Fact]
    public void SessionCrypto_ReplayedCounter_Fails()
    {
        byte[] pairKey = new byte[32];
        RandomNumberGenerator.Fill(pairKey);
        using var alice = new SessionCrypto(pairKey, isInitiator: true, 100);
        using var bob = new SessionCrypto(pairKey, isInitiator: false, 100);

        byte[] sealedMsg = alice.Seal("Click event"u8, out ulong c);

        Assert.True(bob.TryOpen(c, sealedMsg, out _));
        Assert.False(bob.TryOpen(c, sealedMsg, out _)); // replayed
    }

    [Fact]
    public void SessionCrypto_CounterZero_IsRejected()
    {
        byte[] pairKey = new byte[32];
        RandomNumberGenerator.Fill(pairKey);
        using var bob = new SessionCrypto(pairKey, isInitiator: false, 1);

        bool result = bob.TryOpen(0, new byte[32], out _);
        Assert.False(result);
    }

    [Fact]
    public void SessionCrypto_OutOfOrderWithin64_SucceedsOnce()
    {
        byte[] pairKey = new byte[32];
        RandomNumberGenerator.Fill(pairKey);
        using var alice = new SessionCrypto(pairKey, isInitiator: true, 500);
        using var bob = new SessionCrypto(pairKey, isInitiator: false, 500);

        byte[] p1 = alice.Seal("P1"u8, out ulong c1); // c1 = 1
        byte[] p2 = alice.Seal("P2"u8, out ulong c2); // c2 = 2
        byte[] p3 = alice.Seal("P3"u8, out ulong c3); // c3 = 3

        // Receive out of order: 3, then 1, then 2
        Assert.True(bob.TryOpen(c3, p3, out var text3));
        Assert.Equal("P3"u8.ToArray(), text3);

        Assert.True(bob.TryOpen(c1, p1, out var text1));
        Assert.Equal("P1"u8.ToArray(), text1);

        Assert.True(bob.TryOpen(c2, p2, out var text2));
        Assert.Equal("P2"u8.ToArray(), text2);

        // Replaying any out-of-order packet fails
        Assert.False(bob.TryOpen(c1, p1, out _));
        Assert.False(bob.TryOpen(c2, p2, out _));
        Assert.False(bob.TryOpen(c3, p3, out _));
    }

    [Fact]
    public void SessionCrypto_OutOfOrderOlderThan64_Fails()
    {
        byte[] pairKey = new byte[32];
        RandomNumberGenerator.Fill(pairKey);
        using var alice = new SessionCrypto(pairKey, isInitiator: true, 999);
        using var bob = new SessionCrypto(pairKey, isInitiator: false, 999);

        byte[] oldPacket = alice.Seal("Old packet"u8, out ulong cOld); // cOld = 1

        // Advance 70 packets
        ulong cNew = 0;
        byte[] newPacket = Array.Empty<byte>();
        for (int i = 0; i < 70; i++)
        {
            newPacket = alice.Seal("New packet"u8, out cNew);
        }

        // Receive latest packet (counter 71)
        Assert.True(bob.TryOpen(cNew, newPacket, out _));

        // Attempting to deliver old packet (71 - 1 = 70 >= 64) is dropped
        Assert.False(bob.TryOpen(cOld, oldPacket, out _));
    }

    [Fact]
    public void SessionCrypto_DirectionalKeys_CannotReflectPacket()
    {
        byte[] pairKey = new byte[32];
        RandomNumberGenerator.Fill(pairKey);
        using var alice = new SessionCrypto(pairKey, isInitiator: true, 777);

        byte[] sealedMsg = alice.Seal("Reflect me"u8, out ulong counter);

        // Alice sealed with kA2B and receives with kB2A, so Alice trying to open her own packet fails
        bool opened = alice.TryOpen(counter, sealedMsg, out _);
        Assert.False(opened);
    }

    [Fact]
    public void SessionCrypto_RandomGarbage_NeverThrows()
    {
        byte[] pairKey = new byte[32];
        RandomNumberGenerator.Fill(pairKey);
        using var bob = new SessionCrypto(pairKey, isInitiator: false, 1234);

        var random = new Random(42);
        for (int i = 0; i < 20; i++)
        {
            byte[] garbage = new byte[random.Next(0, 100)];
            random.NextBytes(garbage);
            ulong counter = (ulong)random.Next(0, 500);

            bool opened = bob.TryOpen(counter, garbage, out byte[] result);
            Assert.False(opened);
            Assert.Empty(result);
        }
    }

    [Fact]
    public void SessionCrypto_ShortCiphertext_ReturnsFalse()
    {
        byte[] pairKey = new byte[32];
        RandomNumberGenerator.Fill(pairKey);
        using var bob = new SessionCrypto(pairKey, isInitiator: false, 1);

        Assert.False(bob.TryOpen(1, new byte[15], out _));
    }
}
