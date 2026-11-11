using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Mouser.Core.Clipboard;
using Xunit;

namespace Mouser.Tests;

public sealed class ClipboardTests
{
    [Fact]
    public void ClipboardPayload_Serialization_RoundTripsAccurately()
    {
        var original = new ClipboardPayload
        {
            Id = "clip-12345",
            TimestampUtc = 1700000000000,
            ContentType = ClipboardContentType.Code,
            TextContent = "const int value = 42;",
            HtmlContent = "<pre><code>const int value = 42;</code></pre>",
            SourceDevice = "MacBook-Pro",
            IsFavorite = true
        };

        byte[] serialized = original.Serialize();
        Assert.NotEmpty(serialized);

        var restored = ClipboardPayload.Deserialize(serialized);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.TimestampUtc, restored.TimestampUtc);
        Assert.Equal(original.ContentType, restored.ContentType);
        Assert.Equal(original.TextContent, restored.TextContent);
        Assert.Equal(original.HtmlContent, restored.HtmlContent);
        Assert.Equal(original.SourceDevice, restored.SourceDevice);
        Assert.Equal(original.IsFavorite, restored.IsFavorite);
    }

    [Fact]
    public void ClipboardCryptoEngine_EncryptsAndDecryptsAccurately()
    {
        byte[] key = ClipboardCryptoEngine.GenerateKey();
        byte[] message = "Secret confidential clipboard text 12345"u8.ToArray();

        byte[] encrypted = ClipboardCryptoEngine.Encrypt(message, key);
        Assert.NotEqual(message, encrypted);

        byte[] decrypted = ClipboardCryptoEngine.Decrypt(encrypted, key);
        Assert.Equal(message, decrypted);
    }

    [Fact]
    public void ClipboardCryptoEngine_RejectsTamperedCiphertext()
    {
        byte[] key = ClipboardCryptoEngine.GenerateKey();
        byte[] message = "Authentic banking details"u8.ToArray();

        byte[] encrypted = ClipboardCryptoEngine.Encrypt(message, key);

        // Tamper with one byte in the ciphertext
        encrypted[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() =>
        {
            ClipboardCryptoEngine.Decrypt(encrypted, key);
        });
    }

    [Fact]
    public void ClipboardCryptoEngine_RejectsWrongKey()
    {
        byte[] key1 = ClipboardCryptoEngine.GenerateKey();
        byte[] key2 = ClipboardCryptoEngine.GenerateKey();
        byte[] message = "Cross-machine clip"u8.ToArray();

        byte[] encrypted = ClipboardCryptoEngine.Encrypt(message, key1);

        Assert.ThrowsAny<CryptographicException>(() =>
        {
            ClipboardCryptoEngine.Decrypt(encrypted, key2);
        });
    }

    public static TheoryData<string, bool> GetSensitiveTestCases() => new()
    {
        { "ghp_" + "1234567890abcdefghijklmnopqrstuvwxyz", true },
        { "AKIA" + "IOSFODNN7EXAMPLE", true },
        { "sk-" + "1234567890abcdefghijklmnopqrstuvwxyz12345", true },
        { "sk_" + "live_" + "1234567890abcdefghijklmnopqr", true },
        { "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEA0...", true },
        { "Just normal harmless text from a document", false },
        { "https://github.com/mouser/kvm", false }
    };

    [Theory]
    [MemberData(nameof(GetSensitiveTestCases))]
    public void ClipboardPrivacyFilter_DetectsSensitivePatterns(string input, bool expectedSensitive)
    {
        bool isSensitive = ClipboardPrivacyFilter.IsSensitive(input);
        Assert.Equal(expectedSensitive, isSensitive);
    }

    [Fact]
    public void ClipboardPrivacyFilter_MasksSecrets()
    {
        string secret = "ghp_" + "1234567890abcdefghijklmnopqrstuvwxyz";
        string masked = ClipboardPrivacyFilter.Mask(secret);

        Assert.StartsWith("ghp_", masked);
        Assert.EndsWith("wxyz", masked);
        Assert.Contains("••••••••", masked);
    }

    [Fact]
    public async Task ClipboardStorageService_FullTextSearchAndRetention()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"mouser_test_{Guid.NewGuid():N}.db");
        try
        {
            await using var storage = new ClipboardStorageService(tempDb);
            await storage.InitializeAsync();

            var clip1 = new ClipboardPayload
            {
                Id = "clip-1",
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ContentType = ClipboardContentType.Code,
                TextContent = "public void ExecuteMigrationScript()",
                SourceDevice = "MacBook",
                IsFavorite = false
            };

            var clip2 = new ClipboardPayload
            {
                Id = "clip-2",
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10,
                ContentType = ClipboardContentType.Text,
                TextContent = "Weekly grocery list: apples, milk, bread",
                SourceDevice = "Windows-PC",
                IsFavorite = false
            };

            await storage.SaveClipAsync(clip1);
            await storage.SaveClipAsync(clip2);

            // 1. Full text search
            var searchResults = await storage.SearchClipsAsync("Migration");
            Assert.Single(searchResults);
            Assert.Equal("clip-1", searchResults[0].Id);

            var searchGrocery = await storage.SearchClipsAsync("apples");
            Assert.Single(searchGrocery);
            Assert.Equal("clip-2", searchGrocery[0].Id);

            // 2. Favorite toggling
            bool isFavNow = await storage.ToggleFavoriteAsync("clip-1");
            Assert.True(isFavNow);

            // 3. Purge older clips: pass a timestamp in the past (e.g. 10 days) so current clips are NOT purged
            // Unless we specifically test purging by inserting an old clip
            var oldClip = new ClipboardPayload
            {
                Id = "old-clip",
                TimestampUtc = DateTimeOffset.UtcNow.AddDays(-40).ToUnixTimeMilliseconds(),
                ContentType = ClipboardContentType.Text,
                TextContent = "Old clip from 40 days ago",
                SourceDevice = "Old-PC",
                IsFavorite = false
            };
            await storage.SaveClipAsync(oldClip);

            int purged = await storage.PurgeClipsOlderThanAsync(30);
            Assert.Equal(1, purged); // Only old-clip should be purged

            var remaining = await storage.GetRecentClipsAsync();
            Assert.Equal(2, remaining.Count); // clip-1 and clip-2 remain
            Assert.Contains(remaining, c => c.Id == "clip-1");
            Assert.Contains(remaining, c => c.Id == "clip-2");
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }
}
