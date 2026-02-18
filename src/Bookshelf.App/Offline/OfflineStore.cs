using System.Text.Json;
using Bookshelf.Shared.Client;
using Bookshelf.Shared.Contracts.Api;
using Microsoft.Data.Sqlite;

namespace Bookshelf.Offline;

public sealed class OfflineStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public OfflineStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var sql = """
                CREATE TABLE IF NOT EXISTS cache_entries (
                    cache_key TEXT PRIMARY KEY,
                    payload_json TEXT NOT NULL,
                    expires_at_unix_ms INTEGER NULL,
                    updated_at_unix_ms INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS progress_snapshots (
                    user_id INTEGER NOT NULL,
                    book_id INTEGER NOT NULL,
                    media_type TEXT NOT NULL,
                    position_ref TEXT NOT NULL,
                    progress_percent REAL NOT NULL,
                    updated_at_unix_ms INTEGER NOT NULL,
                    PRIMARY KEY (user_id, book_id, media_type)
                );

                CREATE TABLE IF NOT EXISTS history_events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER NOT NULL,
                    book_id INTEGER NOT NULL,
                    media_type TEXT NOT NULL,
                    event_type TEXT NOT NULL,
                    position_ref TEXT NULL,
                    event_at_unix_ms INTEGER NOT NULL,
                    dedupe_key TEXT NOT NULL UNIQUE
                );
                CREATE INDEX IF NOT EXISTS ix_history_events_user_time
                    ON history_events (user_id, event_at_unix_ms DESC, id DESC);

                CREATE TABLE IF NOT EXISTS sync_queue (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    operation_type TEXT NOT NULL,
                    payload_json TEXT NOT NULL,
                    created_at_unix_ms INTEGER NOT NULL,
                    attempts INTEGER NOT NULL DEFAULT 0,
                    next_attempt_unix_ms INTEGER NOT NULL,
                    last_error TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_sync_queue_next_attempt
                    ON sync_queue (next_attempt_unix_ms, id);

                CREATE TABLE IF NOT EXISTS media_index (
                    user_id INTEGER NOT NULL,
                    book_id INTEGER NOT NULL,
                    media_type TEXT NOT NULL,
                    local_path TEXT NOT NULL,
                    is_available INTEGER NOT NULL,
                    updated_at_unix_ms INTEGER NOT NULL,
                    PRIMARY KEY (user_id, book_id, media_type)
                );
                """;

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetCacheAsync<T>(
        string key,
        T payload,
        TimeSpan? ttl,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var expiresAtUnixMs = ttl.HasValue
            ? nowUnixMs + (long)Math.Max(0, ttl.Value.TotalMilliseconds)
            : (long?)null;
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO cache_entries (cache_key, payload_json, expires_at_unix_ms, updated_at_unix_ms)
            VALUES ($key, $payload, $expiresAt, $updatedAt)
            ON CONFLICT(cache_key) DO UPDATE SET
                payload_json = excluded.payload_json,
                expires_at_unix_ms = excluded.expires_at_unix_ms,
                updated_at_unix_ms = excluded.updated_at_unix_ms
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$payload", json);
        command.Parameters.AddWithValue("$expiresAt", (object?)expiresAtUnixMs ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", nowUnixMs);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<T?> GetCacheAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT payload_json, expires_at_unix_ms
            FROM cache_entries
            WHERE cache_key = $key
            """;
        command.Parameters.AddWithValue("$key", key);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return default;
        }

        var payloadJson = reader.GetString(0);
        var expiresAtUnixMs = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1);
        var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (expiresAtUnixMs.HasValue && expiresAtUnixMs.Value < nowUnixMs)
        {
            await DeleteCacheKeyAsync(key, cancellationToken);
            return default;
        }

        return JsonSerializer.Deserialize<T>(payloadJson, JsonOptions);
    }

    public async Task UpsertProgressAsync(ProgressSnapshotDto snapshot, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var updatedAtUnixMs = snapshot.UpdatedAtUtc.ToUnixTimeMilliseconds();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO progress_snapshots (
                user_id,
                book_id,
                media_type,
                position_ref,
                progress_percent,
                updated_at_unix_ms)
            VALUES (
                $userId,
                $bookId,
                $mediaType,
                $positionRef,
                $progressPercent,
                $updatedAt)
            ON CONFLICT(user_id, book_id, media_type) DO UPDATE SET
                position_ref = CASE
                    WHEN excluded.updated_at_unix_ms > progress_snapshots.updated_at_unix_ms
                         OR (
                            excluded.updated_at_unix_ms = progress_snapshots.updated_at_unix_ms
                            AND excluded.progress_percent > progress_snapshots.progress_percent
                         )
                    THEN excluded.position_ref
                    ELSE progress_snapshots.position_ref
                END,
                progress_percent = CASE
                    WHEN excluded.updated_at_unix_ms > progress_snapshots.updated_at_unix_ms
                         OR (
                            excluded.updated_at_unix_ms = progress_snapshots.updated_at_unix_ms
                            AND excluded.progress_percent > progress_snapshots.progress_percent
                         )
                    THEN excluded.progress_percent
                    ELSE progress_snapshots.progress_percent
                END,
                updated_at_unix_ms = CASE
                    WHEN excluded.updated_at_unix_ms > progress_snapshots.updated_at_unix_ms
                         OR (
                            excluded.updated_at_unix_ms = progress_snapshots.updated_at_unix_ms
                            AND excluded.progress_percent > progress_snapshots.progress_percent
                         )
                    THEN excluded.updated_at_unix_ms
                    ELSE progress_snapshots.updated_at_unix_ms
                END
            """;
        command.Parameters.AddWithValue("$userId", snapshot.UserId);
        command.Parameters.AddWithValue("$bookId", snapshot.BookId);
        command.Parameters.AddWithValue("$mediaType", snapshot.MediaType);
        command.Parameters.AddWithValue("$positionRef", snapshot.PositionRef);
        command.Parameters.AddWithValue("$progressPercent", snapshot.ProgressPercent);
        command.Parameters.AddWithValue("$updatedAt", updatedAtUnixMs);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ProgressSnapshotsResponse> ListProgressAsync(
        long userId,
        long? bookId,
        string? mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var offset = (safePage - 1) * safePageSize;
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var whereParts = new List<string> { "user_id = $userId" };
        if (bookId.HasValue)
        {
            whereParts.Add("book_id = $bookId");
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            whereParts.Add("media_type = $mediaType");
        }

        var whereSql = string.Join(" AND ", whereParts);

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM progress_snapshots WHERE {whereSql}";
        countCommand.Parameters.AddWithValue("$userId", userId);
        if (bookId.HasValue)
        {
            countCommand.Parameters.AddWithValue("$bookId", bookId.Value);
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            countCommand.Parameters.AddWithValue("$mediaType", mediaType.Trim().ToLowerInvariant());
        }

        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        await using var dataCommand = connection.CreateCommand();
        dataCommand.CommandText =
            $"""
            SELECT user_id, book_id, media_type, position_ref, progress_percent, updated_at_unix_ms
            FROM progress_snapshots
            WHERE {whereSql}
            ORDER BY updated_at_unix_ms DESC
            LIMIT $limit OFFSET $offset
            """;
        dataCommand.Parameters.AddWithValue("$userId", userId);
        if (bookId.HasValue)
        {
            dataCommand.Parameters.AddWithValue("$bookId", bookId.Value);
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            dataCommand.Parameters.AddWithValue("$mediaType", mediaType.Trim().ToLowerInvariant());
        }

        dataCommand.Parameters.AddWithValue("$limit", safePageSize);
        dataCommand.Parameters.AddWithValue("$offset", offset);

        var items = new List<ProgressSnapshotDto>();
        await using var reader = await dataCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ProgressSnapshotDto(
                UserId: reader.GetInt64(0),
                BookId: reader.GetInt64(1),
                MediaType: reader.GetString(2),
                PositionRef: reader.GetString(3),
                ProgressPercent: Convert.ToDecimal(reader.GetDouble(4)),
                UpdatedAtUtc: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5))));
        }

        return new ProgressSnapshotsResponse(
            Page: safePage,
            PageSize: safePageSize,
            Total: total,
            Items: items);
    }

    public async Task<AppendHistoryEventsResponse> AppendHistoryEventsAsync(
        long userId,
        IReadOnlyList<HistoryEventWriteDto> items,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var added = 0;
        var deduplicated = 0;
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (var item in items)
        {
            var dedupeKey = BuildHistoryDedupeKey(
                userId,
                item.BookId,
                item.MediaType,
                item.EventType,
                item.PositionRef,
                item.EventAtUtc);

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT OR IGNORE INTO history_events (
                    user_id,
                    book_id,
                    media_type,
                    event_type,
                    position_ref,
                    event_at_unix_ms,
                    dedupe_key)
                VALUES (
                    $userId,
                    $bookId,
                    $mediaType,
                    $eventType,
                    $positionRef,
                    $eventAt,
                    $dedupeKey)
                """;
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$bookId", item.BookId);
            command.Parameters.AddWithValue("$mediaType", item.MediaType.Trim().ToLowerInvariant());
            command.Parameters.AddWithValue("$eventType", item.EventType.Trim().ToLowerInvariant());
            command.Parameters.AddWithValue("$positionRef", (object?)NormalizeOptional(item.PositionRef) ?? DBNull.Value);
            command.Parameters.AddWithValue("$eventAt", item.EventAtUtc.ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("$dedupeKey", dedupeKey);

            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected > 0)
            {
                added++;
            }
            else
            {
                deduplicated++;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new AppendHistoryEventsResponse(Added: added, Deduplicated: deduplicated);
    }

    public async Task<HistoryEventsResponse> ListHistoryEventsAsync(
        long userId,
        long? bookId,
        string? mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var offset = (safePage - 1) * safePageSize;
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var whereParts = new List<string> { "user_id = $userId" };
        if (bookId.HasValue)
        {
            whereParts.Add("book_id = $bookId");
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            whereParts.Add("media_type = $mediaType");
        }

        var whereSql = string.Join(" AND ", whereParts);

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM history_events WHERE {whereSql}";
        countCommand.Parameters.AddWithValue("$userId", userId);
        if (bookId.HasValue)
        {
            countCommand.Parameters.AddWithValue("$bookId", bookId.Value);
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            countCommand.Parameters.AddWithValue("$mediaType", mediaType.Trim().ToLowerInvariant());
        }

        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        await using var dataCommand = connection.CreateCommand();
        dataCommand.CommandText =
            $"""
            SELECT id, user_id, book_id, media_type, event_type, position_ref, event_at_unix_ms
            FROM history_events
            WHERE {whereSql}
            ORDER BY event_at_unix_ms DESC, id DESC
            LIMIT $limit OFFSET $offset
            """;
        dataCommand.Parameters.AddWithValue("$userId", userId);
        if (bookId.HasValue)
        {
            dataCommand.Parameters.AddWithValue("$bookId", bookId.Value);
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            dataCommand.Parameters.AddWithValue("$mediaType", mediaType.Trim().ToLowerInvariant());
        }

        dataCommand.Parameters.AddWithValue("$limit", safePageSize);
        dataCommand.Parameters.AddWithValue("$offset", offset);

        var items = new List<HistoryEventDto>();
        await using var reader = await dataCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new HistoryEventDto(
                Id: reader.GetInt64(0),
                UserId: reader.GetInt64(1),
                BookId: reader.GetInt64(2),
                MediaType: reader.GetString(3),
                EventType: reader.GetString(4),
                PositionRef: reader.IsDBNull(5) ? null : reader.GetString(5),
                EventAtUtc: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6))));
        }

        return new HistoryEventsResponse(
            Page: safePage,
            PageSize: safePageSize,
            Total: total,
            Items: items);
    }

    public async Task EnqueueOperationAsync(
        string operationType,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO sync_queue (
                operation_type,
                payload_json,
                created_at_unix_ms,
                attempts,
                next_attempt_unix_ms,
                last_error)
            VALUES (
                $operationType,
                $payloadJson,
                $createdAt,
                0,
                $nextAttempt,
                NULL)
            """;
        command.Parameters.AddWithValue("$operationType", operationType);
        command.Parameters.AddWithValue("$payloadJson", payloadJson);
        command.Parameters.AddWithValue("$createdAt", nowUnixMs);
        command.Parameters.AddWithValue("$nextAttempt", nowUnixMs);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OfflineQueueItem>> ListReadyQueueItemsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, operation_type, payload_json, attempts
            FROM sync_queue
            WHERE next_attempt_unix_ms <= $now
            ORDER BY id
            LIMIT $limit
            """;
        command.Parameters.AddWithValue("$now", nowUnixMs);
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));

        var items = new List<OfflineQueueItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new OfflineQueueItem(
                Id: reader.GetInt64(0),
                OperationType: reader.GetString(1),
                PayloadJson: reader.GetString(2),
                Attempts: reader.GetInt32(3)));
        }

        return items;
    }

    public async Task MarkQueueOperationSucceededAsync(long queueItemId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM sync_queue WHERE id = $id";
        command.Parameters.AddWithValue("$id", queueItemId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkQueueOperationFailedAsync(
        long queueItemId,
        string error,
        int attempts,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var boundedAttempts = Math.Max(1, attempts);
        var nextDelaySeconds = Math.Min(300, (int)Math.Pow(2, Math.Min(8, boundedAttempts)));
        var nextAttemptUnixMs = DateTimeOffset.UtcNow.AddSeconds(nextDelaySeconds).ToUnixTimeMilliseconds();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE sync_queue
            SET attempts = $attempts,
                next_attempt_unix_ms = $nextAttempt,
                last_error = $error
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$attempts", boundedAttempts);
        command.Parameters.AddWithValue("$nextAttempt", nextAttemptUnixMs);
        command.Parameters.AddWithValue("$error", error);
        command.Parameters.AddWithValue("$id", queueItemId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CountQueueItemsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sync_queue";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task UpsertMediaEntryAsync(LocalMediaEntry entry, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO media_index (
                user_id,
                book_id,
                media_type,
                local_path,
                is_available,
                updated_at_unix_ms)
            VALUES (
                $userId,
                $bookId,
                $mediaType,
                $localPath,
                $isAvailable,
                $updatedAt)
            ON CONFLICT(user_id, book_id, media_type) DO UPDATE SET
                local_path = excluded.local_path,
                is_available = excluded.is_available,
                updated_at_unix_ms = excluded.updated_at_unix_ms
            """;
        command.Parameters.AddWithValue("$userId", entry.UserId);
        command.Parameters.AddWithValue("$bookId", entry.BookId);
        command.Parameters.AddWithValue("$mediaType", entry.MediaType.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("$localPath", entry.LocalPath);
        command.Parameters.AddWithValue("$isAvailable", entry.IsAvailable ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", entry.UpdatedAtUtc.ToUnixTimeMilliseconds());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LocalMediaEntry?> GetMediaEntryAsync(
        long userId,
        long bookId,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT user_id, book_id, media_type, local_path, is_available, updated_at_unix_ms
            FROM media_index
            WHERE user_id = $userId AND book_id = $bookId AND media_type = $mediaType
            """;
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$bookId", bookId);
        command.Parameters.AddWithValue("$mediaType", mediaType.Trim().ToLowerInvariant());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LocalMediaEntry(
            UserId: reader.GetInt64(0),
            BookId: reader.GetInt64(1),
            MediaType: reader.GetString(2),
            LocalPath: reader.GetString(3),
            IsAvailable: reader.GetInt32(4) == 1,
            UpdatedAtUtc: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)));
    }

    private async Task DeleteCacheKeyAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM cache_entries WHERE cache_key = $key";
        command.Parameters.AddWithValue("$key", key);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private static string BuildHistoryDedupeKey(
        long userId,
        long bookId,
        string mediaType,
        string eventType,
        string? positionRef,
        DateTimeOffset eventAtUtc)
    {
        return string.Join(
            '|',
            userId,
            bookId,
            mediaType.Trim().ToLowerInvariant(),
            eventType.Trim().ToLowerInvariant(),
            NormalizeOptional(positionRef) ?? string.Empty,
            eventAtUtc.ToUnixTimeMilliseconds());
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record OfflineQueueItem(
    long Id,
    string OperationType,
    string PayloadJson,
    int Attempts);
