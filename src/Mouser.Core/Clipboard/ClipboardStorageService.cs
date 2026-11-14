using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Mouser.Core.Clipboard;

/// <summary>
/// Manages persistent, searchable clipboard history using SQLite and FTS5 full-text indexing.
/// </summary>
public sealed class ClipboardStorageService : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly string? _databasePath;
    private SqliteConnection? _connection;

    public const int MaxTextLength = 50 * 1024 * 1024; // 50 MB
    public const int LargeTextThreshold = 1_000_000;   // 1 MB
    public const int LargeTextDbChars = 20_000;

    public string? DatabasePath => _databasePath;

    public static readonly string[] TagPalette = new[]
    {
        "#6366f1", "#10b981", "#f59e0b", "#ec4899", "#8b5cf6", "#06b6d4", "#f97316", "#14b8a6"
    };

    public static string GetTagColor(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return TagPalette[0];
        var clean = name.Trim().TrimStart('#').Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(clean)) return TagPalette[0];
        uint hash = 2166136261;
        foreach (char c in clean)
        {
            hash = (hash ^ c) * 16777619;
        }
        return TagPalette[hash % (uint)TagPalette.Length];
    }

    public ClipboardStorageService(string? databasePath = null)
    {
        databasePath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".mouser",
            "clipboard.db");

        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _databasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync();

        // Enable WAL mode for high concurrent throughput
        using (var walCmd = _connection.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            await walCmd.ExecuteNonQueryAsync();
        }

        using (var fkCmd = _connection.CreateCommand())
        {
            fkCmd.CommandText = "PRAGMA foreign_keys = ON;";
            await fkCmd.ExecuteNonQueryAsync();
        }

        // Schema creation
        const string schemaSql = @"
            CREATE TABLE IF NOT EXISTS clips (
                id TEXT PRIMARY KEY,
                timestamp INTEGER NOT NULL,
                content_type INTEGER NOT NULL,
                text_content TEXT NOT NULL,
                html_content TEXT,
                source_device TEXT NOT NULL,
                is_favorite INTEGER DEFAULT 0,
                source_app TEXT NOT NULL DEFAULT '',
                media_path TEXT,
                media_hash TEXT,
                width INTEGER,
                height INTEGER,
                bytes INTEGER
            );

            CREATE INDEX IF NOT EXISTS idx_clips_timestamp ON clips(timestamp DESC);

            CREATE VIRTUAL TABLE IF NOT EXISTS clips_fts USING fts5(
                id UNINDEXED,
                text_content,
                source_device UNINDEXED
            );

            CREATE TABLE IF NOT EXISTS tags (
                id TEXT PRIMARY KEY,
                name TEXT UNIQUE COLLATE NOCASE,
                color TEXT,
                is_system INTEGER DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS clip_tags (
                clip_id TEXT,
                tag_id TEXT,
                PRIMARY KEY(clip_id, tag_id),
                FOREIGN KEY(clip_id) REFERENCES clips(id) ON DELETE CASCADE,
                FOREIGN KEY(tag_id) REFERENCES tags(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_clip_tags_clip ON clip_tags(clip_id);
            CREATE INDEX IF NOT EXISTS idx_clip_tags_tag ON clip_tags(tag_id);

            CREATE TABLE IF NOT EXISTS deliveries (
                clip_id TEXT,
                device_id TEXT,
                device_name TEXT,
                status TEXT,
                error TEXT,
                updated INTEGER,
                PRIMARY KEY(clip_id, device_id),
                FOREIGN KEY(clip_id) REFERENCES clips(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_deliveries_clip ON deliveries(clip_id);

            CREATE TABLE IF NOT EXISTS snippets (
                id TEXT PRIMARY KEY,
                name TEXT UNIQUE COLLATE NOCASE,
                body TEXT NOT NULL,
                folder TEXT NOT NULL DEFAULT '',
                favorite INTEGER DEFAULT 0,
                created INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_snippets_order ON snippets(favorite DESC, name ASC);
        ";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = schemaSql;
        await cmd.ExecuteNonQueryAsync();

        // Migration: ensure columns exist in older databases
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var pragmaCmd = _connection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA table_info(clips);";
            using var pragmaReader = await pragmaCmd.ExecuteReaderAsync();
            while (await pragmaReader.ReadAsync())
            {
                existingColumns.Add(pragmaReader.GetString(1));
            }
        }

        if (!existingColumns.Contains("source_app"))
        {
            using var alterCmd = _connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE clips ADD COLUMN source_app TEXT NOT NULL DEFAULT '';";
            await alterCmd.ExecuteNonQueryAsync();
        }

        if (!existingColumns.Contains("media_path"))
        {
            using var alterCmd = _connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE clips ADD COLUMN media_path TEXT;";
            await alterCmd.ExecuteNonQueryAsync();
        }

        if (!existingColumns.Contains("media_hash"))
        {
            using var alterCmd = _connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE clips ADD COLUMN media_hash TEXT;";
            await alterCmd.ExecuteNonQueryAsync();
        }

        if (!existingColumns.Contains("width"))
        {
            using var alterCmd = _connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE clips ADD COLUMN width INTEGER;";
            await alterCmd.ExecuteNonQueryAsync();
        }

        if (!existingColumns.Contains("height"))
        {
            using var alterCmd = _connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE clips ADD COLUMN height INTEGER;";
            await alterCmd.ExecuteNonQueryAsync();
        }

        if (!existingColumns.Contains("bytes"))
        {
            using var alterCmd = _connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE clips ADD COLUMN bytes INTEGER;";
            await alterCmd.ExecuteNonQueryAsync();
        }

        using (var idxCmd = _connection.CreateCommand())
        {
            idxCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_clips_media_hash ON clips(media_hash) WHERE media_hash IS NOT NULL;";
            await idxCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<bool> SaveClipAsync(ClipboardPayload payload)
    {
        if (_connection == null) await InitializeAsync();

        if (payload.ContentType != ClipboardContentType.Image && !string.IsNullOrEmpty(payload.TextContent))
        {
            if (payload.TextContent.Length > MaxTextLength)
            {
                return false;
            }

            if (payload.TextContent.Length > LargeTextThreshold)
            {
                string fullText = payload.TextContent;
                if (string.IsNullOrEmpty(payload.MediaPath))
                {
                    string dbDir = !string.IsNullOrEmpty(_databasePath) ? Path.GetDirectoryName(_databasePath) ?? "" : "";
                    string mediaDir = !string.IsNullOrEmpty(dbDir) ? Path.Combine(dbDir, "media") : Path.Combine(Environment.CurrentDirectory, "media");
                    Directory.CreateDirectory(mediaDir);
                    string filePath = Path.Combine(mediaDir, $"{payload.Id}.txt");
                    await File.WriteAllTextAsync(filePath, fullText);
                    payload.MediaPath = filePath;
                }
                payload.MediaBytes = System.Text.Encoding.UTF8.GetByteCount(fullText);
                payload.TextContent = fullText.Substring(0, LargeTextDbChars);
            }
        }

        if (payload.ContentType == ClipboardContentType.Image && !string.IsNullOrEmpty(payload.MediaHash))
        {
            const string checkImgSql = "SELECT id, media_path FROM clips WHERE media_hash = @hash LIMIT 1;";
            string? existingId = null;
            string? existingMediaPath = null;

            using (var checkImgCmd = _connection!.CreateCommand())
            {
                checkImgCmd.CommandText = checkImgSql;
                checkImgCmd.Parameters.AddWithValue("@hash", payload.MediaHash);
                using var reader = await checkImgCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    existingId = reader.GetString(0);
                    existingMediaPath = reader.IsDBNull(1) ? null : reader.GetString(1);
                }
            }

            if (!string.IsNullOrEmpty(existingId))
            {
                const string updateSql = "UPDATE clips SET timestamp = @time, source_device = @source, source_app = @source_app WHERE id = @id;";
                using var updateCmd = _connection.CreateCommand();
                updateCmd.CommandText = updateSql;
                updateCmd.Parameters.AddWithValue("@time", payload.TimestampUtc);
                updateCmd.Parameters.AddWithValue("@source", payload.SourceDevice);
                updateCmd.Parameters.AddWithValue("@source_app", payload.SourceApp ?? string.Empty);
                updateCmd.Parameters.AddWithValue("@id", existingId);
                await updateCmd.ExecuteNonQueryAsync();

                if (payload.MediaPath != null && payload.MediaPath != existingMediaPath && File.Exists(payload.MediaPath))
                {
                    try
                    {
                        File.Delete(payload.MediaPath);
                    }
                    catch (Exception) { }
                }

                return false;
            }
        }
        else
        {
            // If duplicate text was recently copied, update timestamp instead of bloating database
            const string checkSql = "SELECT id FROM clips WHERE text_content = @text LIMIT 1;";
            using var checkCmd = _connection!.CreateCommand();
            checkCmd.CommandText = checkSql;
            checkCmd.Parameters.AddWithValue("@text", payload.TextContent);
            var existingId = await checkCmd.ExecuteScalarAsync() as string;

            if (!string.IsNullOrEmpty(existingId))
            {
                const string updateSql = "UPDATE clips SET timestamp = @time, source_device = @source, source_app = @source_app, html_content = COALESCE(@html, html_content) WHERE id = @id;";
                using var updateCmd = _connection.CreateCommand();
                updateCmd.CommandText = updateSql;
                updateCmd.Parameters.AddWithValue("@time", payload.TimestampUtc);
                updateCmd.Parameters.AddWithValue("@source", payload.SourceDevice);
                updateCmd.Parameters.AddWithValue("@source_app", payload.SourceApp ?? string.Empty);
                updateCmd.Parameters.AddWithValue("@html", (object?)payload.HtmlContent ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@id", existingId);
                await updateCmd.ExecuteNonQueryAsync();

                if (payload.MediaPath != null && File.Exists(payload.MediaPath))
                {
                    string? existingMediaPath = null;
                    using (var checkPathCmd = _connection.CreateCommand())
                    {
                        checkPathCmd.CommandText = "SELECT media_path FROM clips WHERE id = @id LIMIT 1;";
                        checkPathCmd.Parameters.AddWithValue("@id", existingId);
                        var res = await checkPathCmd.ExecuteScalarAsync();
                        if (res != null && res != DBNull.Value) existingMediaPath = res as string;
                    }
                    if (payload.MediaPath != existingMediaPath)
                    {
                        try { File.Delete(payload.MediaPath); } catch { }
                    }
                }

                return false;
            }
        }

        using var transaction = _connection!.BeginTransaction();
        try
        {
            const string insertClip = @"
                INSERT INTO clips (id, timestamp, content_type, text_content, html_content, source_device, is_favorite, source_app, media_path, media_hash, width, height, bytes)
                VALUES (@id, @timestamp, @content_type, @text_content, @html_content, @source_device, @is_favorite, @source_app, @media_path, @media_hash, @width, @height, @bytes);";

            using var insertCmd = _connection.CreateCommand();
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = insertClip;
            insertCmd.Parameters.AddWithValue("@id", payload.Id);
            insertCmd.Parameters.AddWithValue("@timestamp", payload.TimestampUtc);
            insertCmd.Parameters.AddWithValue("@content_type", (int)payload.ContentType);
            insertCmd.Parameters.AddWithValue("@text_content", payload.TextContent);
            insertCmd.Parameters.AddWithValue("@html_content", (object?)payload.HtmlContent ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@source_device", payload.SourceDevice);
            insertCmd.Parameters.AddWithValue("@is_favorite", payload.IsFavorite ? 1 : 0);
            insertCmd.Parameters.AddWithValue("@source_app", payload.SourceApp ?? string.Empty);
            insertCmd.Parameters.AddWithValue("@media_path", (object?)payload.MediaPath ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@media_hash", (object?)payload.MediaHash ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@width", (object?)payload.Width ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@height", (object?)payload.Height ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@bytes", (object?)payload.MediaBytes ?? DBNull.Value);
            await insertCmd.ExecuteNonQueryAsync();

            const string insertFts = @"
                INSERT INTO clips_fts (id, text_content, source_device)
                VALUES (@id, @text_content, @source_device);";

            using var ftsCmd = _connection.CreateCommand();
            ftsCmd.Transaction = transaction;
            ftsCmd.CommandText = insertFts;
            ftsCmd.Parameters.AddWithValue("@id", payload.Id);
            ftsCmd.Parameters.AddWithValue("@text_content", payload.TextContent);
            ftsCmd.Parameters.AddWithValue("@source_device", payload.SourceDevice);
            await ftsCmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public static string GetFullText(ClipboardPayload payload)
    {
        if (payload == null) return string.Empty;
        if (!string.IsNullOrEmpty(payload.MediaPath) &&
            payload.MediaPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(payload.MediaPath))
        {
            try
            {
                return File.ReadAllText(payload.MediaPath);
            }
            catch { }
        }
        return payload.TextContent;
    }

    public async Task<List<ClipboardPayload>> GetRecentClipsAsync(int limit = 50, int offset = 0, bool favoritesOnly = false)
    {
        if (_connection == null) await InitializeAsync();

        var list = new List<ClipboardPayload>();
        string sql = $@"
            SELECT id, timestamp, content_type, text_content, html_content, source_device, is_favorite, source_app, media_path, media_hash, width, height, bytes
            FROM clips
            {(favoritesOnly ? "WHERE is_favorite = 1" : "")}
            ORDER BY timestamp DESC
            LIMIT @limit OFFSET @offset;";

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadPayload(reader));
        }

        return list;
    }

    public async Task<List<ClipboardPayload>> SearchClipsAsync(string query, int limit = 50, int offset = 0)
    {
        if (_connection == null) await InitializeAsync();

        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetRecentClipsAsync(limit, offset);
        }

        var list = new List<ClipboardPayload>();
        // FTS5 prefix matching
        string sanitizedQuery = query.Trim().Replace("\"", "\"\"") + "*";

        const string sql = @"
            SELECT c.id, c.timestamp, c.content_type, c.text_content, c.html_content, c.source_device, c.is_favorite, c.source_app, c.media_path, c.media_hash, c.width, c.height, c.bytes
            FROM clips c
            JOIN clips_fts f ON c.id = f.id
            WHERE f MATCH @query
            ORDER BY c.timestamp DESC
            LIMIT @limit OFFSET @offset;";

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@query", sanitizedQuery);
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(ReadPayload(reader));
            }
        }
        catch (SqliteException)
        {
            // Fallback to LIKE if query has special syntax
            const string fallbackSql = @"
                SELECT id, timestamp, content_type, text_content, html_content, source_device, is_favorite, source_app, media_path, media_hash, width, height, bytes
                FROM clips
                WHERE text_content LIKE @likeQuery
                ORDER BY timestamp DESC
                LIMIT @limit OFFSET @offset;";

            using var fallbackCmd = _connection.CreateCommand();
            fallbackCmd.CommandText = fallbackSql;
            fallbackCmd.Parameters.AddWithValue("@likeQuery", $"%{query}%");
            fallbackCmd.Parameters.AddWithValue("@limit", limit);
            fallbackCmd.Parameters.AddWithValue("@offset", offset);

            using var reader = await fallbackCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(ReadPayload(reader));
            }
        }

        return list;
    }

    public async Task<bool> ToggleFavoriteAsync(string clipId)
    {
        if (_connection == null) await InitializeAsync();

        using (var updateCmd = _connection!.CreateCommand())
        {
            updateCmd.CommandText = "UPDATE clips SET is_favorite = CASE WHEN is_favorite = 1 THEN 0 ELSE 1 END WHERE id = @id;";
            updateCmd.Parameters.AddWithValue("@id", clipId);
            await updateCmd.ExecuteNonQueryAsync();
        }

        using (var selectCmd = _connection.CreateCommand())
        {
            selectCmd.CommandText = "SELECT is_favorite FROM clips WHERE id = @id;";
            selectCmd.Parameters.AddWithValue("@id", clipId);
            var result = await selectCmd.ExecuteScalarAsync();
            return result is long val && val == 1;
        }
    }

    public async Task<(int Total, int Favorites)> GetCountsAsync()
    {
        if (_connection == null) await InitializeAsync();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(is_favorite), 0) FROM clips;";
        using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    public async Task DeleteClipAsync(string clipId)
    {
        if (_connection == null) await InitializeAsync();

        string? mediaPath = null;
        using (var selectCmd = _connection!.CreateCommand())
        {
            selectCmd.CommandText = "SELECT media_path FROM clips WHERE id = @id LIMIT 1;";
            selectCmd.Parameters.AddWithValue("@id", clipId);
            var result = await selectCmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                mediaPath = result as string;
            }
        }

        using var transaction = _connection.BeginTransaction();
        foreach (var sql in new[] { "DELETE FROM deliveries WHERE clip_id = @id;", "DELETE FROM clip_tags WHERE clip_id = @id;", "DELETE FROM clips WHERE id = @id;", "DELETE FROM clips_fts WHERE id = @id;" })
        {
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@id", clipId);
            await cmd.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();

        if (!string.IsNullOrEmpty(mediaPath) && File.Exists(mediaPath))
        {
            try
            {
                File.Delete(mediaPath);
            }
            catch (Exception) { }
        }
    }

    /// <summary>Deletes history. Starred clips survive unless <paramref name="includeFavorites"/> is set.</summary>
    public async Task<int> ClearAsync(bool includeFavorites = false)
    {
        if (_connection == null) await InitializeAsync();

        string filter = includeFavorites ? "" : " WHERE is_favorite = 0";
        var mediaPathsToDelete = new List<string>();

        using (var selectCmd = _connection!.CreateCommand())
        {
            selectCmd.CommandText = $"SELECT media_path FROM clips{filter};";
            using var reader = await selectCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    var p = reader.GetString(0);
                    if (!string.IsNullOrEmpty(p)) mediaPathsToDelete.Add(p);
                }
            }
        }

        using var transaction = _connection!.BeginTransaction();

        using var delDeliveryCmd = _connection.CreateCommand();
        delDeliveryCmd.Transaction = transaction;
        delDeliveryCmd.CommandText = $"DELETE FROM deliveries WHERE clip_id IN (SELECT id FROM clips{filter});";
        await delDeliveryCmd.ExecuteNonQueryAsync();

        using var tagCmd = _connection.CreateCommand();
        tagCmd.Transaction = transaction;
        tagCmd.CommandText = $"DELETE FROM clip_tags WHERE clip_id IN (SELECT id FROM clips{filter});";
        await tagCmd.ExecuteNonQueryAsync();

        using var ftsCmd = _connection.CreateCommand();
        ftsCmd.Transaction = transaction;
        ftsCmd.CommandText = $"DELETE FROM clips_fts WHERE id IN (SELECT id FROM clips{filter});";
        await ftsCmd.ExecuteNonQueryAsync();

        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"DELETE FROM clips{filter};";
        int count = await cmd.ExecuteNonQueryAsync();

        await transaction.CommitAsync();

        foreach (var path in mediaPathsToDelete)
        {
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception) { }
            }
        }

        return count;
    }

    public Task<int> PurgeClipsOlderThanAsync(int daysToKeep) =>
        PurgeClipsOlderThanAsync(DateTimeOffset.UtcNow.AddDays(-daysToKeep).ToUnixTimeMilliseconds());

    public async Task<int> PurgeClipsOlderThanAsync(long threshold)
    {
        if (_connection == null) await InitializeAsync();

        using var transaction = _connection!.BeginTransaction();
        try
        {
            const string selectOld = "SELECT id, media_path FROM clips WHERE timestamp < @threshold AND is_favorite = 0;";
            var idsToDelete = new List<string>();
            var mediaPathsToDelete = new List<string>();

            using (var selectCmd = _connection.CreateCommand())
            {
                selectCmd.Transaction = transaction;
                selectCmd.CommandText = selectOld;
                selectCmd.Parameters.AddWithValue("@threshold", threshold);
                using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    idsToDelete.Add(reader.GetString(0));
                    if (!reader.IsDBNull(1))
                    {
                        var p = reader.GetString(1);
                        if (!string.IsNullOrEmpty(p)) mediaPathsToDelete.Add(p);
                    }
                }
            }

            if (idsToDelete.Count == 0) return 0;

            const string deleteDeliveries = "DELETE FROM deliveries WHERE clip_id IN (SELECT id FROM clips WHERE timestamp < @threshold AND is_favorite = 0);";
            using var delDeliveryCmd = _connection.CreateCommand();
            delDeliveryCmd.Transaction = transaction;
            delDeliveryCmd.CommandText = deleteDeliveries;
            delDeliveryCmd.Parameters.AddWithValue("@threshold", threshold);
            await delDeliveryCmd.ExecuteNonQueryAsync();

            const string deleteTags = "DELETE FROM clip_tags WHERE clip_id IN (SELECT id FROM clips WHERE timestamp < @threshold AND is_favorite = 0);";
            using var delTagCmd = _connection.CreateCommand();
            delTagCmd.Transaction = transaction;
            delTagCmd.CommandText = deleteTags;
            delTagCmd.Parameters.AddWithValue("@threshold", threshold);
            await delTagCmd.ExecuteNonQueryAsync();

            const string deleteClips = "DELETE FROM clips WHERE timestamp < @threshold AND is_favorite = 0;";
            using var delCmd = _connection.CreateCommand();
            delCmd.Transaction = transaction;
            delCmd.CommandText = deleteClips;
            delCmd.Parameters.AddWithValue("@threshold", threshold);
            int count = await delCmd.ExecuteNonQueryAsync();

            foreach (var id in idsToDelete)
            {
                using var delFts = _connection.CreateCommand();
                delFts.Transaction = transaction;
                delFts.CommandText = "DELETE FROM clips_fts WHERE id = @id;";
                delFts.Parameters.AddWithValue("@id", id);
                await delFts.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            foreach (var path in mediaPathsToDelete)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception) { }
                }
            }

            return count;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<TagInfo>> GetTagsAsync()
    {
        if (_connection == null) await InitializeAsync();

        var list = new List<TagInfo>();

        // System tags derived from ContentType
        int linkCount = 0;
        int codeCount = 0;
        int imageCount = 0;
        int fileCount = 0;

        const string countsSql = "SELECT content_type, COUNT(*) FROM clips GROUP BY content_type;";
        using (var countsCmd = _connection!.CreateCommand())
        {
            countsCmd.CommandText = countsSql;
            using var reader = await countsCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var kind = (ClipboardContentType)reader.GetInt32(0);
                int cnt = reader.GetInt32(1);
                switch (kind)
                {
                    case ClipboardContentType.Link:
                        linkCount = cnt;
                        break;
                    case ClipboardContentType.Code:
                        codeCount = cnt;
                        break;
                    case ClipboardContentType.Image:
                        imageCount = cnt;
                        break;
                    case ClipboardContentType.FileUri:
                        fileCount = cnt;
                        break;
                }
            }
        }

        list.Add(new TagInfo("system:link", "Link", "#06b6d4", linkCount, true));
        list.Add(new TagInfo("system:code", "Code", "#8b5cf6", codeCount, true));
        list.Add(new TagInfo("system:image", "Image", "#10b981", imageCount, true));
        list.Add(new TagInfo("system:file", "File", "#f59e0b", fileCount, true));

        // Custom tags
        const string tagsSql = @"
            SELECT t.id, t.name, t.color, COUNT(ct.clip_id) as cnt
            FROM tags t
            LEFT JOIN clip_tags ct ON t.id = ct.tag_id
            GROUP BY t.id, t.name, t.color
            HAVING cnt > 0
            ORDER BY t.name COLLATE NOCASE ASC;";

        using (var tagsCmd = _connection!.CreateCommand())
        {
            tagsCmd.CommandText = tagsSql;
            using var reader = await tagsCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TagInfo(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    false));
            }
        }

        return list;
    }

    public async Task<TagInfo> AddTagAsync(string clipId, string name)
    {
        if (_connection == null) await InitializeAsync();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name cannot be empty.", nameof(name));

        var cleanName = name.Trim().TrimStart('#').Trim();
        if (string.IsNullOrWhiteSpace(cleanName))
            throw new ArgumentException("Tag name cannot be empty.", nameof(name));

        string tagId;
        string color;
        using (var findCmd = _connection!.CreateCommand())
        {
            findCmd.CommandText = "SELECT id, name, color FROM tags WHERE name = @name COLLATE NOCASE LIMIT 1;";
            findCmd.Parameters.AddWithValue("@name", cleanName);
            using var reader = await findCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                tagId = reader.GetString(0);
                cleanName = reader.GetString(1);
                color = reader.GetString(2);
            }
            else
            {
                tagId = Guid.NewGuid().ToString("N");
                color = GetTagColor(cleanName);
                using var insertCmd = _connection.CreateCommand();
                insertCmd.CommandText = "INSERT INTO tags (id, name, color, is_system) VALUES (@id, @name, @color, 0);";
                insertCmd.Parameters.AddWithValue("@id", tagId);
                insertCmd.Parameters.AddWithValue("@name", cleanName);
                insertCmd.Parameters.AddWithValue("@color", color);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }

        using (var linkCmd = _connection!.CreateCommand())
        {
            linkCmd.CommandText = "INSERT OR IGNORE INTO clip_tags (clip_id, tag_id) VALUES (@clipId, @tagId);";
            linkCmd.Parameters.AddWithValue("@clipId", clipId);
            linkCmd.Parameters.AddWithValue("@tagId", tagId);
            await linkCmd.ExecuteNonQueryAsync();
        }

        int count = 0;
        using (var countCmd = _connection!.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM clip_tags WHERE tag_id = @tagId;";
            countCmd.Parameters.AddWithValue("@tagId", tagId);
            var result = await countCmd.ExecuteScalarAsync();
            if (result is long val) count = (int)val;
        }

        return new TagInfo(tagId, cleanName, color, count, false);
    }

    public async Task RemoveTagAsync(string clipId, string tagIdOrName)
    {
        if (_connection == null) await InitializeAsync();
        if (string.IsNullOrWhiteSpace(tagIdOrName)) return;

        var clean = tagIdOrName.Trim().TrimStart('#').Trim();
        if (string.IsNullOrWhiteSpace(clean)) return;

        string? tagId = null;
        using (var findCmd = _connection!.CreateCommand())
        {
            findCmd.CommandText = "SELECT id FROM tags WHERE id = @val OR name = @val COLLATE NOCASE LIMIT 1;";
            findCmd.Parameters.AddWithValue("@val", clean);
            var result = await findCmd.ExecuteScalarAsync();
            if (result is string id) tagId = id;
        }

        if (!string.IsNullOrEmpty(tagId))
        {
            using var delCmd = _connection!.CreateCommand();
            delCmd.CommandText = "DELETE FROM clip_tags WHERE clip_id = @clipId AND tag_id = @tagId;";
            delCmd.Parameters.AddWithValue("@clipId", clipId);
            delCmd.Parameters.AddWithValue("@tagId", tagId);
            await delCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteTagAsync(string name)
    {
        if (_connection == null) await InitializeAsync();
        if (string.IsNullOrWhiteSpace(name)) return;

        var clean = name.Trim().TrimStart('#').Trim();
        if (string.IsNullOrWhiteSpace(clean)) return;

        string? tagId = null;
        using (var findCmd = _connection!.CreateCommand())
        {
            findCmd.CommandText = "SELECT id FROM tags WHERE id = @val OR name = @val COLLATE NOCASE LIMIT 1;";
            findCmd.Parameters.AddWithValue("@val", clean);
            var result = await findCmd.ExecuteScalarAsync();
            if (result is string id) tagId = id;
        }

        if (!string.IsNullOrEmpty(tagId))
        {
            using var transaction = _connection!.BeginTransaction();
            using (var delLinks = _connection.CreateCommand())
            {
                delLinks.Transaction = transaction;
                delLinks.CommandText = "DELETE FROM clip_tags WHERE tag_id = @tagId;";
                delLinks.Parameters.AddWithValue("@tagId", tagId);
                await delLinks.ExecuteNonQueryAsync();
            }

            using (var delTag = _connection.CreateCommand())
            {
                delTag.Transaction = transaction;
                delTag.CommandText = "DELETE FROM tags WHERE id = @tagId;";
                delTag.Parameters.AddWithValue("@tagId", tagId);
                await delTag.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
    }

    public async Task<IReadOnlyList<TagInfo>> GetClipTagsAsync(string clipId)
    {
        if (_connection == null) await InitializeAsync();

        var list = new List<TagInfo>();

        // System tag derived from ContentType
        int? contentType = null;
        using (var typeCmd = _connection!.CreateCommand())
        {
            typeCmd.CommandText = "SELECT content_type FROM clips WHERE id = @id LIMIT 1;";
            typeCmd.Parameters.AddWithValue("@id", clipId);
            var result = await typeCmd.ExecuteScalarAsync();
            if (result is long val) contentType = (int)val;
        }

        if (contentType.HasValue)
        {
            var kind = (ClipboardContentType)contentType.Value;
            switch (kind)
            {
                case ClipboardContentType.Link:
                    list.Add(new TagInfo("system:link", "Link", "#06b6d4", 1, true));
                    break;
                case ClipboardContentType.Code:
                    list.Add(new TagInfo("system:code", "Code", "#8b5cf6", 1, true));
                    break;
                case ClipboardContentType.Image:
                    list.Add(new TagInfo("system:image", "Image", "#10b981", 1, true));
                    break;
                case ClipboardContentType.FileUri:
                    list.Add(new TagInfo("system:file", "File", "#f59e0b", 1, true));
                    break;
            }
        }

        const string sql = @"
            SELECT t.id, t.name, t.color, (SELECT COUNT(*) FROM clip_tags ct2 WHERE ct2.tag_id = t.id)
            FROM tags t
            JOIN clip_tags ct ON t.id = ct.tag_id
            WHERE ct.clip_id = @clipId
            ORDER BY t.name COLLATE NOCASE ASC;";

        using (var cmd = _connection!.CreateCommand())
        {
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@clipId", clipId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TagInfo(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    false));
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<ClipboardPayload>> GetClipsByTagAsync(string tagName, int limit = 200, int offset = 0)
    {
        if (_connection == null) await InitializeAsync();
        if (string.IsNullOrWhiteSpace(tagName)) return Array.Empty<ClipboardPayload>();

        var clean = tagName.Trim().TrimStart('#').Trim();
        if (string.IsNullOrWhiteSpace(clean)) return Array.Empty<ClipboardPayload>();

        var list = new List<ClipboardPayload>();

        ClipboardContentType? systemType = null;
        if (string.Equals(clean, "Link", StringComparison.OrdinalIgnoreCase))
            systemType = ClipboardContentType.Link;
        else if (string.Equals(clean, "Code", StringComparison.OrdinalIgnoreCase))
            systemType = ClipboardContentType.Code;
        else if (string.Equals(clean, "Image", StringComparison.OrdinalIgnoreCase))
            systemType = ClipboardContentType.Image;
        else if (string.Equals(clean, "File", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(clean, "Files", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(clean, "Folder", StringComparison.OrdinalIgnoreCase))
            systemType = ClipboardContentType.FileUri;

        if (systemType.HasValue)
        {
            const string sql = @"
                SELECT id, timestamp, content_type, text_content, html_content, source_device, is_favorite, source_app, media_path, media_hash, width, height, bytes
                FROM clips
                WHERE content_type = @contentType
                ORDER BY timestamp DESC
                LIMIT @limit OFFSET @offset;";

            using var sysCmd = _connection!.CreateCommand();
            sysCmd.CommandText = sql;
            sysCmd.Parameters.AddWithValue("@contentType", (int)systemType.Value);
            sysCmd.Parameters.AddWithValue("@limit", limit);
            sysCmd.Parameters.AddWithValue("@offset", offset);

            using var sysReader = await sysCmd.ExecuteReaderAsync();
            while (await sysReader.ReadAsync())
            {
                list.Add(ReadPayload(sysReader));
            }
            return list;
        }

        const string customSql = @"
            SELECT c.id, c.timestamp, c.content_type, c.text_content, c.html_content, c.source_device, c.is_favorite, c.source_app, c.media_path, c.media_hash, c.width, c.height, c.bytes
            FROM clips c
            JOIN clip_tags ct ON c.id = ct.clip_id
            JOIN tags t ON ct.tag_id = t.id
            WHERE t.name = @tagName COLLATE NOCASE
            ORDER BY c.timestamp DESC
            LIMIT @limit OFFSET @offset;";

        using var customCmd = _connection!.CreateCommand();
        customCmd.CommandText = customSql;
        customCmd.Parameters.AddWithValue("@tagName", clean);
        customCmd.Parameters.AddWithValue("@limit", limit);
        customCmd.Parameters.AddWithValue("@offset", offset);

        using var customReader = await customCmd.ExecuteReaderAsync();
        while (await customReader.ReadAsync())
        {
            list.Add(ReadPayload(customReader));
        }

        return list;
    }

    private static ClipboardPayload ReadPayload(SqliteDataReader reader)
    {
        var payload = new ClipboardPayload
        {
            Id = reader.GetString(0),
            TimestampUtc = reader.GetInt64(1),
            ContentType = (ClipboardContentType)reader.GetInt32(2),
            TextContent = reader.GetString(3),
            HtmlContent = reader.IsDBNull(4) ? null : reader.GetString(4),
            SourceDevice = reader.GetString(5),
            IsFavorite = reader.GetInt64(6) == 1,
            SourceApp = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetString(7) : string.Empty
        };

        if (reader.FieldCount > 8 && !reader.IsDBNull(8)) payload.MediaPath = reader.GetString(8);
        if (reader.FieldCount > 9 && !reader.IsDBNull(9)) payload.MediaHash = reader.GetString(9);
        if (reader.FieldCount > 10 && !reader.IsDBNull(10)) payload.Width = reader.GetInt32(10);
        if (reader.FieldCount > 11 && !reader.IsDBNull(11)) payload.Height = reader.GetInt32(11);
        if (reader.FieldCount > 12 && !reader.IsDBNull(12)) payload.MediaBytes = reader.GetInt64(12);

        return payload;
    }

    public async Task SaveDeliveriesAsync(string clipId, IReadOnlyList<ClipDelivery> deliveries)
    {
        if (deliveries == null || deliveries.Count == 0) return;
        if (_connection == null) await InitializeAsync();

        long updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        using var transaction = _connection!.BeginTransaction();

        const string sql = @"
            INSERT INTO deliveries (clip_id, device_id, device_name, status, error, updated)
            VALUES (@clipId, @deviceId, @deviceName, @status, @error, @updated)
            ON CONFLICT(clip_id, device_id) DO UPDATE SET
                device_name = excluded.device_name,
                status = excluded.status,
                error = excluded.error,
                updated = excluded.updated;";

        foreach (var d in deliveries)
        {
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@clipId", clipId);
            cmd.Parameters.AddWithValue("@deviceId", d.DeviceId);
            cmd.Parameters.AddWithValue("@deviceName", d.DeviceName);
            cmd.Parameters.AddWithValue("@status", d.Status);
            cmd.Parameters.AddWithValue("@error", (object?)d.Error ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@updated", updated);
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<IReadOnlyList<ClipDelivery>> GetDeliveriesAsync(string clipId)
    {
        if (_connection == null) await InitializeAsync();

        const string sql = "SELECT device_id, device_name, status, error FROM deliveries WHERE clip_id = @clipId ORDER BY updated ASC;";
        var list = new List<ClipDelivery>();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@clipId", clipId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string deviceId = reader.GetString(0);
            string deviceName = reader.GetString(1);
            string status = reader.GetString(2);
            string? error = reader.IsDBNull(3) ? null : reader.GetString(3);
            list.Add(new ClipDelivery(deviceId, deviceName, status, error));
        }

        return list;
    }

    public async Task<IReadOnlyList<Snippet>> GetSnippetsAsync()
    {
        if (_connection == null) await InitializeAsync();

        const string sql = "SELECT id, name, body, folder, favorite, created FROM snippets ORDER BY favorite DESC, name ASC;";
        var list = new List<Snippet>();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Snippet
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Body = reader.GetString(2),
                Folder = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                IsFavorite = reader.GetInt32(4) != 0,
                CreatedUtc = reader.GetInt64(5)
            });
        }

        return list;
    }

    public async Task<Snippet?> GetSnippetByNameAsync(string name)
    {
        if (_connection == null) await InitializeAsync();

        const string sql = "SELECT id, name, body, folder, favorite, created FROM snippets WHERE name = @name COLLATE NOCASE LIMIT 1;";
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@name", name);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Snippet
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Body = reader.GetString(2),
                Folder = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                IsFavorite = reader.GetInt32(4) != 0,
                CreatedUtc = reader.GetInt64(5)
            };
        }

        return null;
    }

    public async Task SaveSnippetAsync(Snippet snippet)
    {
        if (snippet == null) throw new ArgumentNullException(nameof(snippet));
        if (_connection == null) await InitializeAsync();

        const string sql = @"
            INSERT OR REPLACE INTO snippets (id, name, body, folder, favorite, created)
            VALUES (@id, @name, @body, @folder, @favorite, @created);";

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", snippet.Id);
        cmd.Parameters.AddWithValue("@name", snippet.Name);
        cmd.Parameters.AddWithValue("@body", snippet.Body);
        cmd.Parameters.AddWithValue("@folder", snippet.Folder ?? string.Empty);
        cmd.Parameters.AddWithValue("@favorite", snippet.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("@created", snippet.CreatedUtc);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteSnippetAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (_connection == null) await InitializeAsync();

        const string sql = "DELETE FROM snippets WHERE id = @id;";
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task OptimizeAsync()
    {
        if (_connection == null) await InitializeAsync();
        using (var vacuumCmd = _connection!.CreateCommand())
        {
            vacuumCmd.CommandText = "VACUUM;";
            await vacuumCmd.ExecuteNonQueryAsync();
        }
        using (var optCmd = _connection.CreateCommand())
        {
            optCmd.CommandText = "INSERT INTO clips_fts(clips_fts) VALUES('optimize');";
            await optCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task RebuildSearchIndexAsync()
    {
        if (_connection == null) await InitializeAsync();
        using var transaction = _connection!.BeginTransaction();
        try
        {
            using (var delCmd = _connection.CreateCommand())
            {
                delCmd.Transaction = transaction;
                delCmd.CommandText = "DELETE FROM clips_fts;";
                await delCmd.ExecuteNonQueryAsync();
            }
            using (var insertCmd = _connection.CreateCommand())
            {
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = @"
                    INSERT INTO clips_fts (id, text_content, source_device)
                    SELECT id, text_content, source_device FROM clips;";
                await insertCmd.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<long> GetDatabaseSizeBytesAsync()
    {
        await Task.CompletedTask;
        long total = 0;
        if (!string.IsNullOrEmpty(_databasePath) && File.Exists(_databasePath))
        {
            total += new FileInfo(_databasePath).Length;
            if (File.Exists(_databasePath + "-wal")) total += new FileInfo(_databasePath + "-wal").Length;
            if (File.Exists(_databasePath + "-shm")) total += new FileInfo(_databasePath + "-shm").Length;

            var dbDir = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(dbDir))
            {
                var mediaDir = Path.Combine(dbDir, "media");
                if (Directory.Exists(mediaDir))
                {
                    foreach (var file in Directory.EnumerateFiles(mediaDir))
                    {
                        try
                        {
                            total += new FileInfo(file).Length;
                        }
                        catch { }
                    }
                }
            }
        }
        return total;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
