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
    private readonly object _lock = new();

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
                is_favorite INTEGER DEFAULT 0
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
    }

    public async Task<bool> SaveClipAsync(ClipboardPayload payload)
    {
        if (_connection == null) await InitializeAsync();

        // If duplicate text was recently copied, update timestamp instead of bloating database
        const string checkSql = "SELECT id FROM clips WHERE text_content = @text LIMIT 1;";
        using var checkCmd = _connection!.CreateCommand();
        checkCmd.CommandText = checkSql;
        checkCmd.Parameters.AddWithValue("@text", payload.TextContent);
        var existingId = await checkCmd.ExecuteScalarAsync() as string;

        if (!string.IsNullOrEmpty(existingId))
        {
            const string updateSql = "UPDATE clips SET timestamp = @time, source_device = @source WHERE id = @id;";
            using var updateCmd = _connection.CreateCommand();
            updateCmd.CommandText = updateSql;
            updateCmd.Parameters.AddWithValue("@time", payload.TimestampUtc);
            updateCmd.Parameters.AddWithValue("@source", payload.SourceDevice);
            updateCmd.Parameters.AddWithValue("@id", existingId);
            await updateCmd.ExecuteNonQueryAsync();
            return false;
        }

        using var transaction = _connection.BeginTransaction();
        try
        {
            const string insertClip = @"
                INSERT INTO clips (id, timestamp, content_type, text_content, html_content, source_device, is_favorite)
                VALUES (@id, @timestamp, @content_type, @text_content, @html_content, @source_device, @is_favorite);";

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

    public async Task<List<ClipboardPayload>> GetRecentClipsAsync(int limit = 50)
    {
        if (_connection == null) await InitializeAsync();

        var list = new List<ClipboardPayload>();
        const string sql = @"
            SELECT id, timestamp, content_type, text_content, html_content, source_device, is_favorite
            FROM clips
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
            SELECT c.id, c.timestamp, c.content_type, c.text_content, c.html_content, c.source_device, c.is_favorite
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
                SELECT id, timestamp, content_type, text_content, html_content, source_device, is_favorite
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

    public async Task<int> PurgeClipsOlderThanAsync(int daysToKeep)
    {
        if (_connection == null) await InitializeAsync();

        long threshold = DateTimeOffset.UtcNow.AddDays(-daysToKeep).ToUnixTimeMilliseconds();

        using var transaction = _connection!.BeginTransaction();
        try
        {
            const string selectOld = "SELECT id FROM clips WHERE timestamp < @threshold AND is_favorite = 0;";
            var idsToDelete = new List<string>();

            using (var selectCmd = _connection.CreateCommand())
            {
                selectCmd.Transaction = transaction;
                selectCmd.CommandText = selectOld;
                selectCmd.Parameters.AddWithValue("@threshold", threshold);
                using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    idsToDelete.Add(reader.GetString(0));
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
        return new ClipboardPayload
        {
            Id = reader.GetString(0),
            TimestampUtc = reader.GetInt64(1),
            ContentType = (ClipboardContentType)reader.GetInt32(2),
            TextContent = reader.GetString(3),
            HtmlContent = reader.IsDBNull(4) ? null : reader.GetString(4),
            SourceDevice = reader.GetString(5),
            IsFavorite = reader.GetInt64(6) == 1
        };
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
