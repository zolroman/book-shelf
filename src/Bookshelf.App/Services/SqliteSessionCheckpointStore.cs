using Bookshelf.App.Models;
using Microsoft.Data.Sqlite;

namespace Bookshelf.App.Services;

public sealed class SqliteSessionCheckpointStore : ISessionCheckpointStore
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private bool _initialized;

    public SqliteSessionCheckpointStore()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "bookshelf_sessions.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task<ReaderSessionCheckpoint?> GetAsync(
        int userId,
        int bookId,
        string formatType,
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
                SELECT user_id, book_id, format_type, position_ref, progress_percent, current_chapter, current_page,
                       audio_position_seconds, audio_duration_seconds, audio_speed, is_playing,
                       started_event_sent, completed_event_sent, updated_at_utc
                FROM reader_checkpoints
                WHERE user_id = $userId AND book_id = $bookId AND format_type = $formatType
                LIMIT 1
                """;
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$bookId", bookId);
            command.Parameters.AddWithValue("$formatType", NormalizeFormat(formatType));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new ReaderSessionCheckpoint
            {
                UserId = reader.GetInt32(0),
                BookId = reader.GetInt32(1),
                FormatType = reader.GetString(2),
                PositionRef = reader.GetString(3),
                ProgressPercent = reader.GetFloat(4),
                CurrentChapter = reader.GetInt32(5),
                CurrentPage = reader.GetInt32(6),
                AudioPositionSeconds = reader.GetInt32(7),
                AudioDurationSeconds = reader.GetInt32(8),
                AudioSpeed = reader.GetFloat(9),
                IsPlaying = reader.GetInt32(10) == 1,
                StartedEventSent = reader.GetInt32(11) == 1,
                CompletedEventSent = reader.GetInt32(12) == 1,
                UpdatedAtUtc = DateTime.TryParse(reader.GetString(13), out var parsed)
                    ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                    : DateTime.UtcNow
            };
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task UpsertAsync(
        ReaderSessionCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        await EnsureInitializedAsync(cancellationToken);

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO reader_checkpoints (
                    user_id, book_id, format_type, position_ref, progress_percent, current_chapter, current_page,
                    audio_position_seconds, audio_duration_seconds, audio_speed, is_playing,
                    started_event_sent, completed_event_sent, updated_at_utc
                )
                VALUES (
                    $userId, $bookId, $formatType, $positionRef, $progressPercent, $currentChapter, $currentPage,
                    $audioPositionSeconds, $audioDurationSeconds, $audioSpeed, $isPlaying,
                    $startedEventSent, $completedEventSent, $updatedAtUtc
                )
                ON CONFLICT(user_id, book_id, format_type) DO UPDATE SET
                    position_ref = excluded.position_ref,
                    progress_percent = excluded.progress_percent,
                    current_chapter = excluded.current_chapter,
                    current_page = excluded.current_page,
                    audio_position_seconds = excluded.audio_position_seconds,
                    audio_duration_seconds = excluded.audio_duration_seconds,
                    audio_speed = excluded.audio_speed,
                    is_playing = excluded.is_playing,
                    started_event_sent = excluded.started_event_sent,
                    completed_event_sent = excluded.completed_event_sent,
                    updated_at_utc = excluded.updated_at_utc
                """;

            command.Parameters.AddWithValue("$userId", checkpoint.UserId);
            command.Parameters.AddWithValue("$bookId", checkpoint.BookId);
            command.Parameters.AddWithValue("$formatType", NormalizeFormat(checkpoint.FormatType));
            command.Parameters.AddWithValue("$positionRef", checkpoint.PositionRef);
            command.Parameters.AddWithValue("$progressPercent", checkpoint.ProgressPercent);
            command.Parameters.AddWithValue("$currentChapter", checkpoint.CurrentChapter);
            command.Parameters.AddWithValue("$currentPage", checkpoint.CurrentPage);
            command.Parameters.AddWithValue("$audioPositionSeconds", checkpoint.AudioPositionSeconds);
            command.Parameters.AddWithValue("$audioDurationSeconds", checkpoint.AudioDurationSeconds);
            command.Parameters.AddWithValue("$audioSpeed", checkpoint.AudioSpeed);
            command.Parameters.AddWithValue("$isPlaying", checkpoint.IsPlaying ? 1 : 0);
            command.Parameters.AddWithValue("$startedEventSent", checkpoint.StartedEventSent ? 1 : 0);
            command.Parameters.AddWithValue("$completedEventSent", checkpoint.CompletedEventSent ? 1 : 0);
            command.Parameters.AddWithValue("$updatedAtUtc", checkpoint.UpdatedAtUtc.ToUniversalTime().ToString("O"));

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
                CREATE TABLE IF NOT EXISTS reader_checkpoints (
                    user_id INTEGER NOT NULL,
                    book_id INTEGER NOT NULL,
                    format_type TEXT NOT NULL,
                    position_ref TEXT NOT NULL,
                    progress_percent REAL NOT NULL,
                    current_chapter INTEGER NOT NULL,
                    current_page INTEGER NOT NULL,
                    audio_position_seconds INTEGER NOT NULL,
                    audio_duration_seconds INTEGER NOT NULL,
                    audio_speed REAL NOT NULL,
                    is_playing INTEGER NOT NULL,
                    started_event_sent INTEGER NOT NULL,
                    completed_event_sent INTEGER NOT NULL,
                    updated_at_utc TEXT NOT NULL,
                    PRIMARY KEY(user_id, book_id, format_type)
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

    private static string NormalizeFormat(string formatType)
    {
        return string.Equals(formatType, "audio", StringComparison.OrdinalIgnoreCase) ? "audio" : "text";
    }
}
