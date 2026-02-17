using System.Globalization;
using Bookshelf.App.Models;
using Microsoft.Data.Sqlite;

namespace Bookshelf.App.Services;

public sealed class SqliteOfflineStateStore : IOfflineStateStore
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private bool _initialized;

    public SqliteOfflineStateStore()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "bookshelf_offline.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task SaveMetadataAsync(string key, string payloadJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(payloadJson);

        await EnsureInitializedAsync(cancellationToken);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO metadata_cache (cache_key, payload_json, updated_at_utc)
                VALUES ($cacheKey, $payloadJson, $updatedAtUtc)
                ON CONFLICT(cache_key) DO UPDATE SET
                    payload_json = excluded.payload_json,
                    updated_at_utc = excluded.updated_at_utc
                """;
            command.Parameters.AddWithValue("$cacheKey", key);
            command.Parameters.AddWithValue("$payloadJson", payloadJson);
            command.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<string?> LoadMetadataAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await EnsureInitializedAsync(cancellationToken);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT payload_json
                FROM metadata_cache
                WHERE cache_key = $cacheKey
                LIMIT 1
                """;
            command.Parameters.AddWithValue("$cacheKey", key);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result?.ToString();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task EnqueueSyncOperationAsync(
        string operationType,
        string payloadJson,
        string? dedupKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);
        ArgumentNullException.ThrowIfNull(payloadJson);

        await EnsureInitializedAsync(cancellationToken);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var nowUtc = DateTime.UtcNow.ToString("O");
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO pending_sync_operations (
                    operation_type,
                    payload_json,
                    dedup_key,
                    attempts,
                    created_at_utc,
                    updated_at_utc,
                    last_error
                )
                VALUES (
                    $operationType,
                    $payloadJson,
                    $dedupKey,
                    0,
                    $createdAtUtc,
                    $updatedAtUtc,
                    NULL
                )
                ON CONFLICT(dedup_key) DO UPDATE SET
                    operation_type = excluded.operation_type,
                    payload_json = excluded.payload_json,
                    attempts = 0,
                    updated_at_utc = excluded.updated_at_utc,
                    last_error = NULL
                """;

            command.Parameters.AddWithValue("$operationType", operationType);
            command.Parameters.AddWithValue("$payloadJson", payloadJson);
            command.Parameters.AddWithValue("$dedupKey", (object?)dedupKey ?? DBNull.Value);
            command.Parameters.AddWithValue("$createdAtUtc", nowUtc);
            command.Parameters.AddWithValue("$updatedAtUtc", nowUtc);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<SyncOperationRecord>> GetPendingSyncOperationsAsync(
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, operation_type, payload_json, dedup_key, attempts, created_at_utc, updated_at_utc, last_error
                FROM pending_sync_operations
                ORDER BY id
                LIMIT $maxItems
                """;
            command.Parameters.AddWithValue("$maxItems", Math.Max(1, maxItems));

            var result = new List<SyncOperationRecord>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new SyncOperationRecord(
                    Id: reader.GetInt64(0),
                    OperationType: reader.GetString(1),
                    PayloadJson: reader.GetString(2),
                    DedupKey: reader.IsDBNull(3) ? null : reader.GetString(3),
                    Attempts: reader.GetInt32(4),
                    CreatedAtUtc: ParseUtc(reader.GetString(5)),
                    UpdatedAtUtc: ParseUtc(reader.GetString(6)),
                    LastError: reader.IsDBNull(7) ? null : reader.GetString(7)));
            }

            return result;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<int> GetPendingSyncOperationCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM pending_sync_operations";

            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            return scalar is null ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task MarkSyncOperationSucceededAsync(long operationId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM pending_sync_operations
                WHERE id = $operationId
                """;
            command.Parameters.AddWithValue("$operationId", operationId);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task MarkSyncOperationFailedAsync(
        long operationId,
        string error,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE pending_sync_operations
                SET attempts = attempts + 1,
                    updated_at_utc = $updatedAtUtc,
                    last_error = $lastError
                WHERE id = $operationId
                """;
            command.Parameters.AddWithValue("$operationId", operationId);
            command.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$lastError", TruncateError(error));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task UpsertLocalAssetAsync(LocalAssetIndexRecord record, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO local_assets_index (
                    user_id,
                    book_format_id,
                    local_path,
                    file_size_bytes,
                    downloaded_at_utc,
                    deleted_at_utc
                )
                VALUES (
                    $userId,
                    $bookFormatId,
                    $localPath,
                    $fileSizeBytes,
                    $downloadedAtUtc,
                    $deletedAtUtc
                )
                ON CONFLICT(user_id, book_format_id) DO UPDATE SET
                    local_path = excluded.local_path,
                    file_size_bytes = excluded.file_size_bytes,
                    downloaded_at_utc = excluded.downloaded_at_utc,
                    deleted_at_utc = excluded.deleted_at_utc
                """;
            command.Parameters.AddWithValue("$userId", record.UserId);
            command.Parameters.AddWithValue("$bookFormatId", record.BookFormatId);
            command.Parameters.AddWithValue("$localPath", record.LocalPath);
            command.Parameters.AddWithValue("$fileSizeBytes", record.FileSizeBytes);
            command.Parameters.AddWithValue("$downloadedAtUtc", record.DownloadedAtUtc.ToUniversalTime().ToString("O"));
            command.Parameters.AddWithValue("$deletedAtUtc", record.DeletedAtUtc?.ToUniversalTime().ToString("O") ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<LocalAssetIndexRecord>> GetLocalAssetsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT user_id, book_format_id, local_path, file_size_bytes, downloaded_at_utc, deleted_at_utc
                FROM local_assets_index
                WHERE user_id = $userId
                ORDER BY downloaded_at_utc DESC
                """;
            command.Parameters.AddWithValue("$userId", userId);

            var records = new List<LocalAssetIndexRecord>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                records.Add(new LocalAssetIndexRecord(
                    UserId: reader.GetInt32(0),
                    BookFormatId: reader.GetInt32(1),
                    LocalPath: reader.GetString(2),
                    FileSizeBytes: reader.GetInt64(3),
                    DownloadedAtUtc: ParseUtc(reader.GetString(4)),
                    DeletedAtUtc: reader.IsDBNull(5) ? null : ParseUtc(reader.GetString(5))));
            }

            return records;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task MarkLocalAssetDeletedAsync(
        int userId,
        int bookFormatId,
        DateTime deletedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE local_assets_index
                SET deleted_at_utc = $deletedAtUtc
                WHERE user_id = $userId
                  AND book_format_id = $bookFormatId
                """;
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$bookFormatId", bookFormatId);
            command.Parameters.AddWithValue("$deletedAtUtc", deletedAtUtc.ToUniversalTime().ToString("O"));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            SQLitePCL.Batteries_V2.Init();

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS metadata_cache (
                    cache_key TEXT NOT NULL PRIMARY KEY,
                    payload_json TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS pending_sync_operations (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    operation_type TEXT NOT NULL,
                    payload_json TEXT NOT NULL,
                    dedup_key TEXT NULL UNIQUE,
                    attempts INTEGER NOT NULL DEFAULT 0,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL,
                    last_error TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS local_assets_index (
                    user_id INTEGER NOT NULL,
                    book_format_id INTEGER NOT NULL,
                    local_path TEXT NOT NULL,
                    file_size_bytes INTEGER NOT NULL,
                    downloaded_at_utc TEXT NOT NULL,
                    deleted_at_utc TEXT NULL,
                    PRIMARY KEY(user_id, book_format_id)
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);

            _initialized = true;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private static DateTime ParseUtc(string value)
    {
        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        return DateTime.UtcNow;
    }

    private static string TruncateError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return "Unknown sync error";
        }

        return error.Length <= 400 ? error : error[..400];
    }
}
