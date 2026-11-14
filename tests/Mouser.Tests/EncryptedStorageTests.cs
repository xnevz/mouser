using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Mouser.Core.Clipboard;
using Xunit;

namespace Mouser.Tests;

public sealed class EncryptedStorageTests
{
    [Fact]
    public async Task EncryptedStorage_CannotBeOpenedWithoutKey()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"mouser_enc_test_{Guid.NewGuid():N}.db");
        byte[] key = RandomNumberGenerator.GetBytes(32);

        try
        {
            // 1. Create and populate encrypted DB
            await using (var storage = new ClipboardStorageService(dbPath, key))
            {
                await storage.InitializeAsync();
                var clip = new ClipboardPayload
                {
                    Id = "clip-enc-1",
                    TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ContentType = ClipboardContentType.Text,
                    TextContent = "Top secret credentials",
                    SourceDevice = "HostA"
                };
                await storage.SaveClipAsync(clip);
            }

            // 2. Attempt to open without key
            await using (var unencStorage = new ClipboardStorageService(dbPath, key: null))
            {
                var ex = await Assert.ThrowsAsync<SqliteException>(async () =>
                {
                    await unencStorage.InitializeAsync();
                });
                Assert.Contains("file is not a database", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDeleteFile(dbPath);
            TryDeleteFile(dbPath + "-wal");
            TryDeleteFile(dbPath + "-shm");
        }
    }

    [Fact]
    public async Task EncryptedStorage_CannotBeOpenedWithWrongKey()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"mouser_enc_test_{Guid.NewGuid():N}.db");
        byte[] correctKey = RandomNumberGenerator.GetBytes(32);
        byte[] wrongKey = RandomNumberGenerator.GetBytes(32);

        try
        {
            // 1. Create DB with correct key
            await using (var storage = new ClipboardStorageService(dbPath, correctKey))
            {
                await storage.InitializeAsync();
                var clip = new ClipboardPayload
                {
                    Id = "clip-enc-2",
                    TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ContentType = ClipboardContentType.Text,
                    TextContent = "Classified documents",
                    SourceDevice = "HostB"
                };
                await storage.SaveClipAsync(clip);
            }

            // 2. Attempt to open with wrong key
            await using (var wrongStorage = new ClipboardStorageService(dbPath, wrongKey))
            {
                var ex = await Assert.ThrowsAsync<SqliteException>(async () =>
                {
                    await wrongStorage.InitializeAsync();
                });
                Assert.Contains("file is not a database", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDeleteFile(dbPath);
            TryDeleteFile(dbPath + "-wal");
            TryDeleteFile(dbPath + "-shm");
        }
    }

    [Fact]
    public async Task EncryptedStorage_FullTextSearchAndOperations_WorkWithKey()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"mouser_enc_test_{Guid.NewGuid():N}.db");
        byte[] key = RandomNumberGenerator.GetBytes(32);

        try
        {
            await using var storage = new ClipboardStorageService(dbPath, key);
            await storage.InitializeAsync();

            var clip1 = new ClipboardPayload
            {
                Id = "clip-1",
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ContentType = ClipboardContentType.Code,
                TextContent = "void DecryptDatabaseWithSqlCipher()",
                SourceDevice = "Station-1",
                IsFavorite = false
            };
            var clip2 = new ClipboardPayload
            {
                Id = "clip-2",
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 50,
                ContentType = ClipboardContentType.Text,
                TextContent = "Super secure encryption token",
                SourceDevice = "Station-2",
                IsFavorite = true
            };

            await storage.SaveClipAsync(clip1);
            await storage.SaveClipAsync(clip2);

            // 1. FTS Search
            var searchResults = await storage.SearchClipsAsync("SqlCipher");
            Assert.Single(searchResults);
            Assert.Equal("clip-1", searchResults[0].Id);

            var searchToken = await storage.SearchClipsAsync("encryption");
            Assert.Single(searchToken);
            Assert.Equal("clip-2", searchToken[0].Id);

            // 2. Favorite toggle
            bool isFavNow = await storage.ToggleFavoriteAsync("clip-1");
            Assert.True(isFavNow);

            // 3. Counts
            var (allCount, starredCount) = await storage.GetCountsAsync();
            Assert.Equal(2, allCount);
            Assert.Equal(2, starredCount);

            // 4. Tags
            var tag = await storage.AddTagAsync("clip-1", "Security");
            Assert.NotNull(tag);
            var tags = await storage.GetClipTagsAsync("clip-1");
            Assert.Contains(tags, t => t.Name == "Security");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDeleteFile(dbPath);
            TryDeleteFile(dbPath + "-wal");
            TryDeleteFile(dbPath + "-shm");
        }
    }

    [Fact]
    public async Task EncryptedStorage_MigrationFromPlaintext_PreservesAllData()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"mouser_enc_test_{Guid.NewGuid():N}.db");
        byte[] key = RandomNumberGenerator.GetBytes(32);

        try
        {
            // 1. Create a plaintext DB without key
            await using (var plainStorage = new ClipboardStorageService(dbPath, key: null))
            {
                await plainStorage.InitializeAsync();

                var clip1 = new ClipboardPayload
                {
                    Id = "clip-plain-1",
                    TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ContentType = ClipboardContentType.Text,
                    TextContent = "Original unencrypted clipboard item",
                    SourceDevice = "PlainStation",
                    IsFavorite = true
                };
                var clip2 = new ClipboardPayload
                {
                    Id = "clip-plain-2",
                    TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 20,
                    ContentType = ClipboardContentType.Code,
                    TextContent = "function migrate() { return true; }",
                    SourceDevice = "PlainStation",
                    IsFavorite = false
                };

                await plainStorage.SaveClipAsync(clip1);
                await plainStorage.SaveClipAsync(clip2);
                await plainStorage.AddTagAsync("clip-plain-1", "MigratedTag");
            }

            // 2. Open with key -> Should trigger automatic SQLCipher migration
            await using (var encStorage = new ClipboardStorageService(dbPath, key: key))
            {
                await encStorage.InitializeAsync();

                var (allCount, starredCount) = await encStorage.GetCountsAsync();
                Assert.Equal(2, allCount);
                Assert.Equal(1, starredCount);

                var searchResults = await encStorage.SearchClipsAsync("unencrypted");
                Assert.Single(searchResults);
                Assert.Equal("clip-plain-1", searchResults[0].Id);
                Assert.True(searchResults[0].IsFavorite);

                var tags = await encStorage.GetClipTagsAsync("clip-plain-1");
                Assert.Single(tags);
                Assert.Equal("MigratedTag", tags[0].Name);

                var codeResults = await encStorage.SearchClipsAsync("migrate");
                Assert.Single(codeResults);
                Assert.Equal("clip-plain-2", codeResults[0].Id);
            }

            // 3. Verify on disk that DB is now encrypted and cannot be opened without key
            await using (var unencStorage = new ClipboardStorageService(dbPath, key: null))
            {
                var ex = await Assert.ThrowsAsync<SqliteException>(async () =>
                {
                    await unencStorage.InitializeAsync();
                });
                Assert.Contains("file is not a database", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDeleteFile(dbPath);
            TryDeleteFile(dbPath + ".bak");
            TryDeleteFile(dbPath + "-wal");
            TryDeleteFile(dbPath + "-shm");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }
}
