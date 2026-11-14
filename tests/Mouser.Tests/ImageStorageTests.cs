using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Mouser.Core.Clipboard;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public sealed class ImageStorageTests : IDisposable
{
    private readonly string _tempDir;

    public ImageStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mouser_img_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }

    private static byte[] CreatePngBytes(int width, int height, int extraLength = 0)
    {
        // 8 byte PNG signature + 4 byte chunk length + 4 byte chunk type + 4 byte width + 4 byte height + extra
        var bytes = new byte[24 + extraLength];
        // PNG header
        bytes[0] = 0x89;
        bytes[1] = 0x50;
        bytes[2] = 0x4E;
        bytes[3] = 0x47;
        bytes[4] = 0x0D;
        bytes[5] = 0x0A;
        bytes[6] = 0x1A;
        bytes[7] = 0x0A;

        // IHDR chunk length (13) & type "IHDR"
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8, 4), 13);
        bytes[12] = (byte)'I';
        bytes[13] = (byte)'H';
        bytes[14] = (byte)'D';
        bytes[15] = (byte)'R';

        // Dimensions
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);

        return bytes;
    }

    [Fact]
    public void PngHelper_GetDimensions_ValidPng_ReturnsExpectedWidthAndHeight()
    {
        var png1x1 = CreatePngBytes(1, 1);
        var (w1, h1) = PngHelper.GetDimensions(png1x1);
        Assert.Equal(1, w1);
        Assert.Equal(1, h1);

        var pngFhd = CreatePngBytes(1920, 1080);
        var (w2, h2) = PngHelper.GetDimensions(pngFhd);
        Assert.Equal(1920, w2);
        Assert.Equal(1080, h2);
    }

    [Fact]
    public void PngHelper_GetDimensions_InvalidBytes_ReturnsZero()
    {
        // Empty bytes
        Assert.Equal((0, 0), PngHelper.GetDimensions(ReadOnlySpan<byte>.Empty));

        // Too short (< 24 bytes)
        var shortBytes = new byte[20];
        Assert.Equal((0, 0), PngHelper.GetDimensions(shortBytes));

        // Invalid signature
        var invalidSignature = new byte[30];
        invalidSignature[0] = 0xFF; // JPEG-like
        invalidSignature[1] = 0xD8;
        Assert.Equal((0, 0), PngHelper.GetDimensions(invalidSignature));
    }

    [Fact]
    public void ClipboardPayload_MediaProperties_SerializeAndDeserialize()
    {
        var payload = new ClipboardPayload
        {
            Id = "img-test-1",
            TimestampUtc = 1234567890L,
            ContentType = ClipboardContentType.Image,
            TextContent = "Image 640×480",
            SourceDevice = "DeviceA",
            SourceApp = "SnippingTool",
            MediaPath = "C:\\path\\to\\test.png",
            MediaHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Width = 640,
            Height = 480,
            MediaBytes = 98765L
        };

        var bytes = payload.Serialize();
        var deserialized = ClipboardPayload.Deserialize(bytes);

        Assert.Equal(payload.Id, deserialized.Id);
        Assert.Equal(payload.TimestampUtc, deserialized.TimestampUtc);
        Assert.Equal(ClipboardContentType.Image, deserialized.ContentType);
        Assert.Equal("Image 640×480", deserialized.TextContent);
        Assert.Equal("DeviceA", deserialized.SourceDevice);
        Assert.Equal("SnippingTool", deserialized.SourceApp);
        Assert.Equal("C:\\path\\to\\test.png", deserialized.MediaPath);
        Assert.Equal(payload.MediaHash, deserialized.MediaHash);
        Assert.Equal(640, deserialized.Width);
        Assert.Equal(480, deserialized.Height);
        Assert.Equal(98765L, deserialized.MediaBytes);
    }

    [Fact]
    public async Task ClipboardStorageService_SaveAndReadImageClip_PersistsAllColumns()
    {
        var dbPath = Path.Combine(_tempDir, "storage_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var payload = new ClipboardPayload
        {
            Id = "img-persisted-1",
            TimestampUtc = 100000L,
            ContentType = ClipboardContentType.Image,
            TextContent = "Image 800×600",
            SourceDevice = "PC1",
            SourceApp = "Paint",
            MediaPath = Path.Combine(_tempDir, "test.png"),
            MediaHash = "aabbccddeeff00112233",
            Width = 800,
            Height = 600,
            MediaBytes = 54321L
        };

        bool saved = await storage.SaveClipAsync(payload);
        Assert.True(saved);

        var recent = await storage.GetRecentClipsAsync(10);
        var clip = Assert.Single(recent);

        Assert.Equal("img-persisted-1", clip.Id);
        Assert.Equal(ClipboardContentType.Image, clip.ContentType);
        Assert.Equal("Image 800×600", clip.TextContent);
        Assert.Equal("Paint", clip.SourceApp);
        Assert.Equal(payload.MediaPath, clip.MediaPath);
        Assert.Equal("aabbccddeeff00112233", clip.MediaHash);
        Assert.Equal(800, clip.Width);
        Assert.Equal(600, clip.Height);
        Assert.Equal(54321L, clip.MediaBytes);

        // Search test
        var searchResults = await storage.SearchClipsAsync("800");
        Assert.Single(searchResults);
    }

    [Fact]
    public async Task ClipboardStorageService_DeduplicationByMediaHash_UpdatesTimestampAndDeletesRedundantFile()
    {
        var dbPath = Path.Combine(_tempDir, "dedup_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var originalPath = Path.Combine(_tempDir, "orig.png");
        await File.WriteAllBytesAsync(originalPath, new byte[] { 1, 2, 3 });

        var payload1 = new ClipboardPayload
        {
            Id = "clip-1",
            TimestampUtc = 1000L,
            ContentType = ClipboardContentType.Image,
            TextContent = "Image 100×100",
            MediaPath = originalPath,
            MediaHash = "same_hash_123",
            SourceApp = "OldApp",
            Width = 100,
            Height = 100,
            MediaBytes = 3
        };

        Assert.True(await storage.SaveClipAsync(payload1));

        // Redundant new file created before deduplication check
        var redundantPath = Path.Combine(_tempDir, "redundant.png");
        await File.WriteAllBytesAsync(redundantPath, new byte[] { 1, 2, 3 });

        var payload2 = new ClipboardPayload
        {
            Id = "clip-2",
            TimestampUtc = 2000L,
            ContentType = ClipboardContentType.Image,
            TextContent = "Image 100×100",
            MediaPath = redundantPath,
            MediaHash = "same_hash_123",
            SourceApp = "NewApp",
            Width = 100,
            Height = 100,
            MediaBytes = 3
        };

        // Saving duplicate with same MediaHash should return false
        Assert.False(await storage.SaveClipAsync(payload2));

        // The redundant file on disk should be removed
        Assert.False(File.Exists(redundantPath));
        // The original file should remain
        Assert.True(File.Exists(originalPath));

        // Only 1 row in database, with updated timestamp and source app
        var clips = await storage.GetRecentClipsAsync(10);
        var stored = Assert.Single(clips);
        Assert.Equal("clip-1", stored.Id);
        Assert.Equal(2000L, stored.TimestampUtc);
        Assert.Equal("NewApp", stored.SourceApp);
        Assert.Equal(originalPath, stored.MediaPath);
    }

    [Fact]
    public async Task ClipboardStorageService_DeleteClipAsync_DeletesMediaFileFromDisk()
    {
        var dbPath = Path.Combine(_tempDir, "delete_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var mediaFile = Path.Combine(_tempDir, "delete_me.png");
        await File.WriteAllBytesAsync(mediaFile, new byte[] { 1, 2, 3, 4 });

        var payload = new ClipboardPayload
        {
            Id = "clip-to-delete",
            ContentType = ClipboardContentType.Image,
            TextContent = "Image 10×10",
            MediaPath = mediaFile,
            MediaHash = "delete_hash"
        };

        await storage.SaveClipAsync(payload);
        Assert.True(File.Exists(mediaFile));

        await storage.DeleteClipAsync("clip-to-delete");

        var clips = await storage.GetRecentClipsAsync(10);
        Assert.Empty(clips);
        Assert.False(File.Exists(mediaFile));
    }

    [Fact]
    public async Task ClipboardStorageService_PurgeClipsOlderThanAsync_DeletesUnstarredMediaFiles()
    {
        var dbPath = Path.Combine(_tempDir, "purge_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long oldTime = DateTimeOffset.UtcNow.AddDays(-15).ToUnixTimeMilliseconds();

        var oldUnstarredFile = Path.Combine(_tempDir, "old_unstarred.png");
        var oldStarredFile = Path.Combine(_tempDir, "old_starred.png");
        var recentFile = Path.Combine(_tempDir, "recent.png");

        await File.WriteAllBytesAsync(oldUnstarredFile, new byte[] { 1 });
        await File.WriteAllBytesAsync(oldStarredFile, new byte[] { 2 });
        await File.WriteAllBytesAsync(recentFile, new byte[] { 3 });

        // 1. Old unstarred clip
        await storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "old-unstarred",
            TimestampUtc = oldTime,
            ContentType = ClipboardContentType.Image,
            TextContent = "Old unstarred",
            MediaPath = oldUnstarredFile,
            MediaHash = "hash-1",
            IsFavorite = false
        });

        // 2. Old starred clip
        await storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "old-starred",
            TimestampUtc = oldTime,
            ContentType = ClipboardContentType.Image,
            TextContent = "Old starred",
            MediaPath = oldStarredFile,
            MediaHash = "hash-2",
            IsFavorite = true
        });

        // 3. Recent clip
        await storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "recent-clip",
            TimestampUtc = now,
            ContentType = ClipboardContentType.Image,
            TextContent = "Recent",
            MediaPath = recentFile,
            MediaHash = "hash-3",
            IsFavorite = false
        });

        // Purge clips older than 7 days
        int purged = await storage.PurgeClipsOlderThanAsync(7);
        Assert.Equal(1, purged);

        Assert.False(File.Exists(oldUnstarredFile));
        Assert.True(File.Exists(oldStarredFile));
        Assert.True(File.Exists(recentFile));
    }

    [Fact]
    public async Task ClipboardStorageService_ClearAsync_DeletesMediaFiles()
    {
        var dbPath = Path.Combine(_tempDir, "clear_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var file1 = Path.Combine(_tempDir, "clear1.png");
        var file2 = Path.Combine(_tempDir, "clear2.png");
        await File.WriteAllBytesAsync(file1, new byte[] { 1 });
        await File.WriteAllBytesAsync(file2, new byte[] { 2 });

        await storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "clear-1",
            ContentType = ClipboardContentType.Image,
            TextContent = "Image 1",
            MediaPath = file1,
            MediaHash = "h1",
            IsFavorite = false
        });

        await storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "clear-2",
            ContentType = ClipboardContentType.Image,
            TextContent = "Image 2",
            MediaPath = file2,
            MediaHash = "h2",
            IsFavorite = true
        });

        // Clear without favorites
        int cleared = await storage.ClearAsync(includeFavorites: false);
        Assert.Equal(1, cleared);

        Assert.False(File.Exists(file1));
        Assert.True(File.Exists(file2));

        // Clear all including favorites
        int clearedRemaining = await storage.ClearAsync(includeFavorites: true);
        Assert.Equal(1, clearedRemaining);
        Assert.False(File.Exists(file2));
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_PollClipboardAsync_CapturesImageFromFakeClipboard()
    {
        var dbPath = Path.Combine(_tempDir, "vm_test.db");
        var settingsPath = Path.Combine(_tempDir, "settings.json");

        var settings = new SettingsStore(settingsPath);
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var vm = new ClipboardHistoryViewModel(storage, settings);
        var fakeClipboard = new FakeClipboard();
        vm.AttachClipboard(fakeClipboard);

        byte[] pngBytes = CreatePngBytes(320, 240, 100);
        fakeClipboard.ImagePng = pngBytes;
        fakeClipboard.Text = null;

        bool added = await vm.PollClipboardAsync();
        Assert.True(added);

        var clip = Assert.Single(vm.FilteredClips);
        Assert.True(clip.IsImage);
        Assert.Equal(320, clip.Width);
        Assert.Equal(240, clip.Height);
        Assert.Equal(pngBytes.Length, clip.MediaBytes);
        Assert.Equal("Image 320×240", clip.Content);
        Assert.NotNull(clip.MediaPath);
        Assert.True(File.Exists(clip.MediaPath));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(pngBytes)).ToLowerInvariant(), clip.MediaHash);

        // Polling again with unchanged clipboard should return false
        bool polledAgain = await vm.PollClipboardAsync();
        Assert.False(polledAgain);
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_PollClipboardAsync_SkipsOversizedImage()
    {
        var dbPath = Path.Combine(_tempDir, "oversized_test.db");
        var settingsPath = Path.Combine(_tempDir, "settings.json");

        var settings = new SettingsStore(settingsPath);
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var vm = new ClipboardHistoryViewModel(storage, settings);
        var fakeClipboard = new FakeClipboard();
        vm.AttachClipboard(fakeClipboard);

        // Create > 20 MB image (e.g. 21 MB)
        fakeClipboard.ImagePng = new byte[21 * 1024 * 1024];
        fakeClipboard.Text = null;

        bool added = await vm.PollClipboardAsync();
        Assert.False(added);
        Assert.Equal("Skipped a very large image.", vm.StatusMessage);
        Assert.Empty(vm.FilteredClips);
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_PollClipboardAsync_RespectsSaveImagesSetting()
    {
        var dbPath = Path.Combine(_tempDir, "save_disabled_test.db");
        var settingsPath = Path.Combine(_tempDir, "settings.json");

        var settings = new SettingsStore(settingsPath);
        settings.Current.SaveImages = false;
        settings.Save();

        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var vm = new ClipboardHistoryViewModel(storage, settings);
        var fakeClipboard = new FakeClipboard();
        vm.AttachClipboard(fakeClipboard);

        fakeClipboard.ImagePng = CreatePngBytes(100, 100);
        fakeClipboard.Text = null;

        bool added = await vm.PollClipboardAsync();
        Assert.False(added);
        Assert.Empty(vm.FilteredClips);
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_CopyClipAsync_CopiesImageSnapshotToClipboard()
    {
        var dbPath = Path.Combine(_tempDir, "copy_img_test.db");
        var settingsPath = Path.Combine(_tempDir, "settings.json");

        var settings = new SettingsStore(settingsPath);
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var vm = new ClipboardHistoryViewModel(storage, settings);
        var fakeClipboard = new FakeClipboard();
        vm.AttachClipboard(fakeClipboard);

        var mediaFile = Path.Combine(_tempDir, "copy_target.png");
        byte[] expectedBytes = CreatePngBytes(50, 50);
        await File.WriteAllBytesAsync(mediaFile, expectedBytes);

        var payload = new ClipboardPayload
        {
            Id = "img-to-recopy",
            ContentType = ClipboardContentType.Image,
            TextContent = "Image 50×50",
            MediaPath = mediaFile,
            MediaHash = "hash50",
            Width = 50,
            Height = 50,
            MediaBytes = expectedBytes.Length
        };

        var itemVm = new ClipboardItemViewModel(payload);
        Assert.True(itemVm.IsImage);
        Assert.Equal(50, itemVm.Width);
        Assert.Equal(50, itemVm.Height);
        Assert.Equal(expectedBytes.Length, itemVm.MediaBytes);

        await vm.CopyClipCommand.ExecuteAsync(itemVm);

        Assert.Equal("Copied to clipboard.", vm.StatusMessage);
        var snapshot = await fakeClipboard.GetSnapshotAsync();
        Assert.NotNull(snapshot.ImagePng);
        Assert.Equal(expectedBytes, snapshot.ImagePng);
    }

    [Fact]
    public void SettingsViewModel_SaveImages_UpdatesSettingAndStore()
    {
        var settingsPath = Path.Combine(_tempDir, "settings_vm_test.json");
        var store = new SettingsStore(settingsPath);
        using var host = new MouserHost(store, new RecordingSink(), null, 0, _tempDir);
        var vm = new SettingsViewModel(store, host);

        Assert.True(vm.SaveImages);

        vm.SaveImages = false;

        Assert.False(store.Current.SaveImages);

        // Reload to verify file persistence
        var reloaded = new SettingsStore(settingsPath);
        Assert.False(reloaded.Current.SaveImages);
    }

    [Fact]
    public void ClipboardPayload_Deserialize_LegacyFormatWithoutMedia_DefaultsPropertiesToNull()
    {
        using var ms = new MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms, System.Text.Encoding.UTF8);
        writer.Write("legacy-clip-8");
        writer.Write(1700000000000L);
        writer.Write((int)ClipboardContentType.Text);
        writer.Write("Legacy text");
        writer.Write(string.Empty);
        writer.Write("Device-1");
        writer.Write(false);
        writer.Write("App-1");
        writer.Flush();

        var payload = ClipboardPayload.Deserialize(ms.ToArray());

        Assert.Equal("legacy-clip-8", payload.Id);
        Assert.Equal("App-1", payload.SourceApp);
        Assert.Null(payload.MediaPath);
        Assert.Null(payload.MediaHash);
        Assert.Null(payload.Width);
        Assert.Null(payload.Height);
        Assert.Null(payload.MediaBytes);
    }

    [Fact]
    public async Task ClipboardStorageService_MigratesOlderSchema_AddsMediaColumnsSuccessfully()
    {
        var tempDb = Path.Combine(_tempDir, "mig_media_test.db");

        // Create table without media columns
        var connStr = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = tempDb,
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate
        }.ToString();

        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(connStr))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE clips (
                    id TEXT PRIMARY KEY,
                    timestamp INTEGER NOT NULL,
                    content_type INTEGER NOT NULL,
                    text_content TEXT NOT NULL,
                    html_content TEXT,
                    source_device TEXT NOT NULL,
                    is_favorite INTEGER DEFAULT 0,
                    source_app TEXT NOT NULL DEFAULT ''
                );
                CREATE INDEX idx_clips_timestamp ON clips(timestamp DESC);
                CREATE VIRTUAL TABLE clips_fts USING fts5(id UNINDEXED, text_content, source_device UNINDEXED);
            ";
            await cmd.ExecuteNonQueryAsync();
        }

        // Now open with ClipboardStorageService which should migrate all columns
        await using (var storage = new ClipboardStorageService(tempDb))
        {
            await storage.InitializeAsync();

            var imgPayload = new ClipboardPayload
            {
                Id = "migrated-img-1",
                ContentType = ClipboardContentType.Image,
                TextContent = "Image 200×200",
                MediaPath = Path.Combine(_tempDir, "mig.png"),
                MediaHash = "mig_hash",
                Width = 200,
                Height = 200,
                MediaBytes = 1024
            };

            Assert.True(await storage.SaveClipAsync(imgPayload));

            var clips = await storage.GetRecentClipsAsync();
            var clip = Assert.Single(clips);
            Assert.Equal("migrated-img-1", clip.Id);
            Assert.Equal(200, clip.Width);
            Assert.Equal(200, clip.Height);
            Assert.Equal(1024, clip.MediaBytes);
            Assert.Equal("mig_hash", clip.MediaHash);
            Assert.Equal(imgPayload.MediaPath, clip.MediaPath);
        }
    }
}
