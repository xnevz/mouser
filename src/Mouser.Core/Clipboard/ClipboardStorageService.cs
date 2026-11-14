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
    private SqliteConnection? _connection;

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

    public async Task<List<ClipboardPayload>> GetRecentClipsAsync(int limit = 50, bool favoritesOnly = false)
    {
        if (_connection == null) await InitializeAsync();

        var list = new List<ClipboardPayload>();
        string sql = $@"
            SELECT id, timestamp, content_type, text_content, html_content, source_device, is_favorite, source_app, media_path, media_hash, width, height, bytes
            FROM clips
            {(favoritesOnly ? "WHERE is_favorite = 1" : "")}
            ORDER BY timestamp DESC
            LIMIT @limit;";

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadPayload(reader));
        }

        return list;
    }

    public async Task<List<ClipboardPayload>> SearchClipsAsync(string query, int limit = 50)
    {
        if (_connection == null) await InitializeAsync();

        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetRecentClipsAsync(limit);
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
            LIMIT @limit;";

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@query", sanitizedQuery);
        cmd.Parameters.AddWithValue("@limit", limit);

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
                LIMIT @limit;";

            using var fallbackCmd = _connection.CreateCommand();
            fallbackCmd.CommandText = fallbackSql;
            fallbackCmd.Parameters.AddWithValue("@likeQuery", $"%{query}%");
            fallbackCmd.Parameters.AddWithValue("@limit", limit);

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
        foreach (var sql in new[] { "DELETE FROM clips WHERE id = @id;", "DELETE FROM clips_fts WHERE id = @id;" })
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
