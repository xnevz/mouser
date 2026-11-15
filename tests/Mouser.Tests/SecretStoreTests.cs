using System;
using System.Security.Cryptography;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;


public class SecretStoreTests
{
    [Fact]
    public void Protect_NullOrEmpty_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, SecretStore.Protect(null));
        Assert.Equal(string.Empty, SecretStore.Protect(Array.Empty<byte>()));
    }

    [Fact]
    public void Unprotect_NullOrWhitespace_ReturnsNull()
    {
        Assert.Null(SecretStore.Unprotect(null));
        Assert.Null(SecretStore.Unprotect(string.Empty));
        Assert.Null(SecretStore.Unprotect("   "));
    }

    [Fact]
    public void Unprotect_InvalidBase64_ReturnsNull()
    {
        Assert.Null(SecretStore.Unprotect("not-a-valid-base64-string!@#$"));
    }

    [WindowsFact]
    public void Protect_Unprotect_RoundTrip_OnWindows()
    {
        byte[] secret = RandomNumberGenerator.GetBytes(32);
        string protectedText = SecretStore.Protect(secret);

        Assert.False(string.IsNullOrWhiteSpace(protectedText));
        // Verify it was actually encrypted (not raw base64)
        Assert.NotEqual(Convert.ToBase64String(secret), protectedText);

        byte[]? recovered = SecretStore.Unprotect(protectedText);
        Assert.NotNull(recovered);
        Assert.Equal(secret, recovered);
    }

    [WindowsFact]
    public void Unprotect_TamperedInput_ReturnsNull()
    {
        byte[] secret = RandomNumberGenerator.GetBytes(32);
        string protectedText = SecretStore.Protect(secret);

        byte[] cipherBytes = Convert.FromBase64String(protectedText);
        // Tamper with one byte in the ciphertext/header
        cipherBytes[cipherBytes.Length / 2] ^= 0xFF;
        string tampered = Convert.ToBase64String(cipherBytes);

        byte[]? recovered = SecretStore.Unprotect(tampered);
        Assert.Null(recovered);
    }

    [WindowsFact]
    public void Unprotect_TruncatedCiphertext_ReturnsNull()
    {
        byte[] secret = RandomNumberGenerator.GetBytes(32);
        string protectedText = SecretStore.Protect(secret);

        byte[] cipherBytes = Convert.FromBase64String(protectedText);
        byte[] truncated = new byte[cipherBytes.Length / 2];
        Array.Copy(cipherBytes, truncated, truncated.Length);

        byte[]? recovered = SecretStore.Unprotect(Convert.ToBase64String(truncated));
        Assert.Null(recovered);
    }

    [WindowsFact]
    public async Task PeerSettings_UnprotectFailure_TreatedAsUnpaired_ShowsPairAgain()
    {
        await using var app = new AppFixture();
        string peerId = "peer-fail-1";
        // Corrupt pair key that fails unprotect
        app.Settings.Current.Peers[peerId] = new PeerSettings
        {
            Name = "Laptop-Corrupt",
            PairKeyProtected = Convert.ToBase64String(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 })
        };
        app.Settings.Save();

        var vm = new DevicesViewModel(app.Host, app.Settings);
        var device = vm.Devices.FirstOrDefault(d => d.Id == peerId);

        Assert.NotNull(device);
        Assert.False(device.IsPaired);
        Assert.Equal("Pair again", device.StatusText);
        Assert.Equal("Pair again", device.PairButtonText);
    }

    [WindowsFact]
    public async Task PeerSettings_ValidProtectedKey_IsPaired_And_StatusNormal()
    {
        await using var app = new AppFixture();
        string peerId = "peer-ok-1";
        byte[] key = new byte[32];
        key[0] = 0x42;
        app.Settings.Current.Peers[peerId] = new PeerSettings
        {
            Name = "Laptop-OK",
            PairKeyProtected = SecretStore.Protect(key)
        };
        app.Settings.Save();

        var vm = new DevicesViewModel(app.Host, app.Settings);
        var device = vm.Devices.FirstOrDefault(d => d.Id == peerId);

        Assert.NotNull(device);
        Assert.True(device.IsPaired);
        Assert.Equal("Offline", device.StatusText);
        Assert.Equal("Connect & pair", device.PairButtonText);
    }
}
