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

    [Theory]
    [InlineData("https://github.com/xyz/repo", ClipboardContentType.Link)]
    [InlineData("  http://example.com/a?b=c  ", ClipboardContentType.Link)]
    [InlineData("var x = 42;", ClipboardContentType.Code)]
    [InlineData("public void Run()\n{\n    Console.WriteLine(1);\n}", ClipboardContentType.Code)]
    [InlineData("import os\nprint(os.getcwd())", ClipboardContentType.Code)]
    [InlineData("Buy milk and eggs on the way home", ClipboardContentType.Text)]
    [InlineData("Dear team,\nthe meeting moved to 3pm.\nThanks", ClipboardContentType.Text)]
    [InlineData("see https://example.com for details", ClipboardContentType.Text)]
    public void Classifier_DetectsContentType(string text, ClipboardContentType expected)
    {
        Assert.Equal(expected, ClipboardClassifier.Classify(text));
    }

    [Fact]
    public void Classifier_Title_UsesFirstNonEmptyLineAndTruncates()
    {
        Assert.Equal("hello", ClipboardClassifier.Title("\n\n  hello  \nworld"));
        Assert.Equal(80, ClipboardClassifier.Title(new string('a', 200)).Length);
    }

    [Fact]
    public void PrivacyFilter_DetectsValidCardNumbersOnly()
    {
        Assert.True(ClipboardPrivacyFilter.IsSensitive("4111 1111 1111 1111"));  // Luhn-valid test card
        Assert.False(ClipboardPrivacyFilter.IsSensitive("1234 5678 9012 3456")); // fails Luhn
    }

    [Fact]
    public async Task Storage_DeleteClearCountsAndFavorites()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"mouser_test_{Guid.NewGuid():N}.db");
        try
        {
            await using var storage = new ClipboardStorageService(tempDb);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (int i = 0; i < 5; i++)
                await storage.SaveClipAsync(new ClipboardPayload { Id = $"c{i}", TimestampUtc = now + i, TextContent = $"clip number {i}" });

            // Copying the same text again bumps it instead of duplicating it.
            Assert.False(await storage.SaveClipAsync(new ClipboardPayload { TimestampUtc = now + 100, TextContent = "clip number 0" }));
            Assert.Equal("c0", (await storage.GetRecentClipsAsync(1))[0].Id);

            await storage.ToggleFavoriteAsync("c1");
            Assert.Equal((5, 1), await storage.GetCountsAsync());
            Assert.Equal("c1", Assert.Single(await storage.GetRecentClipsAsync(10, favoritesOnly: true)).Id);

            await storage.DeleteClipAsync("c2");
            Assert.Empty(await storage.SearchClipsAsync("number 2"));

            int removed = await storage.ClearAsync();
            Assert.Equal(3, removed);
            Assert.Equal((1, 1), await storage.GetCountsAsync());
            Assert.Empty(await storage.SearchClipsAsync("number 3")); // full-text index cleared too
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var f in new[] { tempDb, tempDb + "-wal", tempDb + "-shm" })
                if (File.Exists(f)) try { File.Delete(f); } catch { }
        }
    }
}
