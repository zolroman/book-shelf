using Bookshelf.App.Models;
using Bookshelf.Shared.Contracts.History;
using Bookshelf.Shared.Contracts.Progress;
using Microsoft.Extensions.Logging;

namespace Bookshelf.App.Services;

public sealed class ReadingSessionService(
    IBookshelfApiClient apiClient,
    ISessionCheckpointStore checkpointStore,
    IOfflineSyncService offlineSyncService,
    ILogger<ReadingSessionService> logger) : IReadingSessionService
{
    private readonly IBookshelfApiClient _apiClient = apiClient;
    private readonly ISessionCheckpointStore _checkpointStore = checkpointStore;
    private readonly IOfflineSyncService _offlineSyncService = offlineSyncService;
    private readonly ILogger<ReadingSessionService> _logger = logger;

    public async Task<ReaderSessionCheckpoint> LoadAsync(
        int userId,
        int bookId,
        string formatType,
        CancellationToken cancellationToken = default)
    {
        var normalizedFormat = NormalizeFormat(formatType);
        var localCheckpoint = await _checkpointStore.GetAsync(userId, bookId, normalizedFormat, cancellationToken);

        var remote = await _apiClient.GetProgressAsync(userId, bookId, normalizedFormat, cancellationToken);
        if (remote is null)
        {
            return localCheckpoint ?? CreateDefault(userId, bookId, normalizedFormat);
        }

        var remoteCheckpoint = CreateDefault(userId, bookId, normalizedFormat);
        remoteCheckpoint.PositionRef = remote.PositionRef;
        remoteCheckpoint.ProgressPercent = remote.ProgressPercent;
        remoteCheckpoint.UpdatedAtUtc = remote.UpdatedAtUtc;

        if (normalizedFormat == "text")
        {
            ParseTextPosition(remote.PositionRef, remoteCheckpoint);
        }
        else
        {
            ParseAudioPosition(remote.PositionRef, remoteCheckpoint);
        }

        var merged = SelectNewer(localCheckpoint, remoteCheckpoint);
        await _checkpointStore.UpsertAsync(merged, cancellationToken);
        return merged;
    }

    public async Task SaveCheckpointAsync(
        ReaderSessionCheckpoint checkpoint,
        bool syncRemote,
        CancellationToken cancellationToken = default)
    {
        checkpoint.UpdatedAtUtc = DateTime.UtcNow;
        checkpoint.FormatType = NormalizeFormat(checkpoint.FormatType);

        await _checkpointStore.UpsertAsync(checkpoint, cancellationToken);

        if (!syncRemote)
        {
            return;
        }

        _ = await _offlineSyncService.QueueProgressAsync(
            new UpsertProgressRequest(
                checkpoint.UserId,
                checkpoint.BookId,
                checkpoint.FormatType,
                checkpoint.PositionRef,
                checkpoint.ProgressPercent),
            checkpoint.UpdatedAtUtc,
            cancellationToken);
    }

    public async Task MarkStartedAsync(
        ReaderSessionCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        if (checkpoint.StartedEventSent)
        {
            return;
        }

        var request = new AddHistoryEventRequest(
            checkpoint.UserId,
            checkpoint.BookId,
            NormalizeFormat(checkpoint.FormatType),
            "started",
            checkpoint.PositionRef,
            DateTime.UtcNow);

        var result = await _offlineSyncService.QueueHistoryEventAsync(request, cancellationToken);
        checkpoint.StartedEventSent = result || checkpoint.StartedEventSent;
        await SaveCheckpointAsync(checkpoint, syncRemote: true, cancellationToken);
    }

    public async Task MarkCompletedAsync(
        ReaderSessionCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        if (checkpoint.CompletedEventSent)
        {
            return;
        }

        checkpoint.ProgressPercent = 100;
        checkpoint.UpdatedAtUtc = DateTime.UtcNow;

        var request = new AddHistoryEventRequest(
            checkpoint.UserId,
            checkpoint.BookId,
            NormalizeFormat(checkpoint.FormatType),
            "completed",
            checkpoint.PositionRef,
            DateTime.UtcNow);

        var result = await _offlineSyncService.QueueHistoryEventAsync(request, cancellationToken);
        checkpoint.CompletedEventSent = result || checkpoint.CompletedEventSent;
        await SaveCheckpointAsync(checkpoint, syncRemote: true, cancellationToken);
    }

    private static ReaderSessionCheckpoint CreateDefault(int userId, int bookId, string formatType)
    {
        return new ReaderSessionCheckpoint
        {
            UserId = userId,
            BookId = bookId,
            FormatType = formatType,
            PositionRef = formatType == "text" ? "c1:p1" : "0",
            ProgressPercent = 0,
            AudioDurationSeconds = 0,
            AudioPositionSeconds = 0,
            CurrentChapter = 1,
            CurrentPage = 1,
            AudioSpeed = 1f
        };
    }

    private static ReaderSessionCheckpoint SelectNewer(
        ReaderSessionCheckpoint? localCheckpoint,
        ReaderSessionCheckpoint remoteCheckpoint)
    {
        if (localCheckpoint is null)
        {
            return remoteCheckpoint;
        }

        return localCheckpoint.UpdatedAtUtc >= remoteCheckpoint.UpdatedAtUtc
            ? localCheckpoint
            : remoteCheckpoint;
    }

    private static string NormalizeFormat(string formatType)
    {
        return string.Equals(formatType, "audio", StringComparison.OrdinalIgnoreCase) ? "audio" : "text";
    }

    private void ParseTextPosition(string? positionRef, ReaderSessionCheckpoint checkpoint)
    {
        if (string.IsNullOrWhiteSpace(positionRef))
        {
            checkpoint.CurrentChapter = 1;
            checkpoint.CurrentPage = 1;
            checkpoint.PositionRef = "c1:p1";
            return;
        }

        try
        {
            var segments = positionRef.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var segment in segments)
            {
                if (segment.StartsWith("c", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(segment[1..], out var chapter))
                {
                    checkpoint.CurrentChapter = Math.Max(1, chapter);
                }
                else if (segment.StartsWith("p", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(segment[1..], out var page))
                {
                    checkpoint.CurrentPage = Math.Max(1, page);
                }
            }

            checkpoint.PositionRef = $"c{checkpoint.CurrentChapter}:p{checkpoint.CurrentPage}";
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to parse text position '{PositionRef}'.", positionRef);
            checkpoint.CurrentChapter = 1;
            checkpoint.CurrentPage = 1;
            checkpoint.PositionRef = "c1:p1";
        }
    }

    private static void ParseAudioPosition(string? positionRef, ReaderSessionCheckpoint checkpoint)
    {
        if (int.TryParse(positionRef, out var seconds))
        {
            checkpoint.AudioPositionSeconds = Math.Max(0, seconds);
            checkpoint.PositionRef = checkpoint.AudioPositionSeconds.ToString();
            return;
        }

        checkpoint.AudioPositionSeconds = 0;
        checkpoint.PositionRef = "0";
    }
}
