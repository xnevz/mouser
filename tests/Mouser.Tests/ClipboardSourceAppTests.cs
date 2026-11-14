using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Mouser.Core.Clipboard;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public sealed class ClipboardSourceAppTests
{
    [Fact]
    public void Payload_RoundTrip_WithSourceApp()
    {
        var original = new ClipboardPayload
        {
            Id = "clip-app-1",
            TimestampUtc = 1700000000000,
            ContentType = ClipboardContentType.Text,
            TextContent = "Hello from Notepad",
            HtmlContent = null,
            SourceDevice = "Workstation",
            IsFavorite = false,
            SourceApp = "notepad"
        };

        byte[] serialized = original.Serialize();
        Assert.NotEmpty(serialized);

        var restored = ClipboardPayload.Deserialize(serialized);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.TimestampUtc, restored.TimestampUtc);
        Assert.Equal(original.ContentType, restored.ContentType);
        Assert.Equal(original.TextContent, restored.TextContent);
        Assert.Equal(original.SourceDevice, restored.SourceDevice);
        Assert.Equal(original.IsFavorite, restored.IsFavorite);
        Assert.Equal("notepad", restored.SourceApp);
    }

    [Fact]
    public void Payload_Deserialize_OldFormatWithoutSourceApp_DefaultsToEmpty()
    {
        // Old 7-field wire format without trailing SourceApp string
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);
        writer.Write("clip-old-7");
        writer.Write(1690000000000L);
        writer.Write((int)ClipboardContentType.Text);
        writer.Write("Legacy text content");
        writer.Write(string.Empty);
        writer.Write("Legacy-Device");
        writer.Write(false);
        writer.Flush();

        byte[] oldBytes = ms.ToArray();
        var payload = ClipboardPayload.Deserialize(oldBytes);

        Assert.Equal("clip-old-7", payload.Id);
        Assert.Equal("Legacy text content", payload.TextContent);
        Assert.Equal("Legacy-Device", payload.SourceDevice);
        Assert.False(payload.IsFavorite);
        Assert.Equal(string.Empty, payload.SourceApp);
    }

    [Fact]
    public async Task Storage_MigratesOldSchema_PreservesAndSavesSourceApp()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"mouser_mig_{Guid.NewGuid():N}.db");
        try
        {
            // Create database with legacy schema (no source_app column)
            var connStr = new SqliteConnectionStringBuilder
            {
                DataSource = tempDb,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            using (var conn = new SqliteConnection(connStr))
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
                        is_favorite INTEGER DEFAULT 0
                    );

                    CREATE INDEX idx_clips_timestamp ON clips(timestamp DESC);

                    CREATE VIRTUAL TABLE clips_fts USING fts5(
                        id UNINDEXED,
                        text_content,
                        source_device UNINDEXED
                    );

                    INSERT INTO clips (id, timestamp, content_type, text_content, html_content, source_device, is_favorite)
                    VALUES ('legacy-clip-1', 1700000000000, 0, 'Legacy content before migration', NULL, 'Desktop-1', 0);
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // Open via ClipboardStorageService; InitializeAsync should add source_app
            await using (var storage = new ClipboardStorageService(tempDb))
            {
                await storage.InitializeAsync();

                // 1. Verify old clip loads and has empty SourceApp
                var existingClips = await storage.GetRecentClipsAsync();
                var legacyClip = Assert.Single(existingClips);
                Assert.Equal("legacy-clip-1", legacyClip.Id);
                Assert.Equal(string.Empty, legacyClip.SourceApp);

                // 2. Save new clip with SourceApp
                var newClip = new ClipboardPayload
                {
                    Id = "new-clip-2",
                    TimestampUtc = 1700000010000,
                    ContentType = ClipboardContentType.Code,
                    TextContent = "var x = 100;",
                    SourceDevice = "Desktop-1",
                    IsFavorite = true,
                    SourceApp = "devenv"
                };
                Assert.True(await storage.SaveClipAsync(newClip));

                // 3. Retrieve and assert SourceApp is preserved
                var allClips = await storage.GetRecentClipsAsync();
                Assert.Equal(2, allClips.Count);

                var retrievedNew = allClips.First(c => c.Id == "new-clip-2");
                Assert.Equal("devenv", retrievedNew.SourceApp);

                // 4. Search clips and ensure SourceApp is populated in search results
                var search = await storage.SearchClipsAsync("var");
                var searchedClip = Assert.Single(search);
                Assert.Equal("new-clip-2", searchedClip.Id);
                Assert.Equal("devenv", searchedClip.SourceApp);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var f in new[] { tempDb, tempDb + "-wal", tempDb + "-shm" })
            {
                if (File.Exists(f))
                {
                    try { File.Delete(f); } catch { }
                }
            }
        }
    }

    [Fact]
    public async Task Capture_UsesAppProbe_SetsSourceApp()
    {
        await using var app = new AppFixture();
        var history = app.Main.ClipboardHistory;
        await history.InitializeAsync();

        // Inject fake AppProbe
        history.AppProbe = () => ("code", "Mouser - Visual Studio Code");

        bool captured = await history.CaptureAsync("Console.WriteLine(\"Hello\");");
        Assert.True(captured);

        var clipVm = Assert.Single(history.FilteredClips);
        Assert.Equal("code", clipVm.SourceApp);
        Assert.True(clipVm.HasSourceApp);
    }

    [Fact]
    public async Task PollClipboard_UsesAppProbe_SetsSourceApp()
    {
        await using var app = new AppFixture();
        var history = app.Main.ClipboardHistory;
        await history.InitializeAsync();

        history.AppProbe = () => ("firefox", "Mozilla Firefox");
        app.Clipboard.Text = "https://developer.mozilla.org";

        bool polled = await history.PollClipboardAsync();
        Assert.True(polled);

        var clipVm = Assert.Single(history.FilteredClips);
        Assert.Equal("firefox", clipVm.SourceApp);
        Assert.True(clipVm.HasSourceApp);
    }

    [Fact]
    public void ClipboardItemViewModel_SourceApp_ExposesProperties()
    {
        var withApp = new ClipboardItemViewModel(new ClipboardPayload
        {
            TextContent = "sample",
            SourceApp = "slack"
        });
        Assert.Equal("slack", withApp.SourceApp);
        Assert.True(withApp.HasSourceApp);

        var withoutApp = new ClipboardItemViewModel(new ClipboardPayload
        {
            TextContent = "sample",
            SourceApp = ""
        });
        Assert.Equal("", withoutApp.SourceApp);
        Assert.False(withoutApp.HasSourceApp);

        var nullApp = new ClipboardItemViewModel(new ClipboardPayload
        {
            TextContent = "sample",
            SourceApp = "   "
        });
        Assert.False(nullApp.HasSourceApp);
    }

    [Fact]
    public void ForegroundApp_Get_ReturnsTupleWithoutThrowing()
    {
        // Safe to call anywhere; on Windows it calls user32, on non-Windows it returns ("", "")
        var (app, title) = ForegroundApp.Get();
        Assert.NotNull(app);
        Assert.NotNull(title);
    }
}
