using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Entities;

public sealed class DownloadJob
{
    private static readonly IReadOnlySet<DownloadJobStatus> TerminalStates =
        new HashSet<DownloadJobStatus>
        {
            DownloadJobStatus.Completed,
            DownloadJobStatus.Failed,
            DownloadJobStatus.Canceled,
        };

    private DownloadJob()
    {
    }

    public DownloadJob(long userId, long bookId, MediaType mediaType, string source, string? torrentMagnet)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Download source is required.", nameof(source));
        }

        UserId = userId;
        BookId = bookId;
        MediaType = mediaType;
        Source = source.Trim();
        TorrentMagnet = NormalizeOptional(torrentMagnet);
        Status = DownloadJobStatus.Queued;
        var nowUtc = DateTimeOffset.UtcNow;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public long Id { get; private set; }

    public long UserId { get; private set; }

    public User? User { get; private set; }

    public long BookId { get; private set; }

    public Book? Book { get; private set; }

    public MediaType MediaType { get; private set; }

    public string Source { get; private set; } = string.Empty;

    public string? ExternalJobId { get; private set; }

    public string? TorrentMagnet { get; private set; }

    public DownloadJobStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public DateTimeOffset? FirstNotFoundAtUtc { get; private set; }

    public string? FailureReason { get; private set; }

    public void SetExternalJobId(string? externalJobId, DateTimeOffset updatedAtUtc)
    {
        ExternalJobId = NormalizeOptional(externalJobId);
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetNotFoundObserved(DateTimeOffset observedAtUtc)
    {
        FirstNotFoundAtUtc ??= observedAtUtc;
        UpdatedAtUtc = observedAtUtc;
    }

    public void ClearNotFoundObserved(DateTimeOffset updatedAtUtc)
    {
        FirstNotFoundAtUtc = null;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void TransitionTo(DownloadJobStatus nextStatus, DateTimeOffset updatedAtUtc, string? failureReason = null)
    {
        if (Status == nextStatus)
        {
            UpdatedAtUtc = updatedAtUtc;
            if (nextStatus == DownloadJobStatus.Failed)
            {
                FailureReason = NormalizeOptional(failureReason);
            }

            return;
        }

        if (!CanTransition(Status, nextStatus))
        {
            throw new InvalidOperationException($"Invalid transition: {Status} -> {nextStatus}.");
        }

        Status = nextStatus;
        UpdatedAtUtc = updatedAtUtc;

        if (nextStatus == DownloadJobStatus.Completed)
        {
            CompletedAtUtc = updatedAtUtc;
            FailureReason = null;
            FirstNotFoundAtUtc = null;
            return;
        }

        if (nextStatus == DownloadJobStatus.Failed)
        {
            FailureReason = NormalizeOptional(failureReason);
            CompletedAtUtc = null;
            return;
        }

        if (nextStatus == DownloadJobStatus.Canceled)
        {
            CompletedAtUtc = null;
            FailureReason = null;
            return;
        }

        FailureReason = null;
    }

    public static bool CanTransition(DownloadJobStatus current, DownloadJobStatus next)
    {
        if (TerminalStates.Contains(current))
        {
            return false;
        }

        return current switch
        {
            DownloadJobStatus.Queued => next is DownloadJobStatus.Downloading or DownloadJobStatus.Failed or DownloadJobStatus.Canceled,
            DownloadJobStatus.Downloading => next is DownloadJobStatus.Completed or DownloadJobStatus.Failed or DownloadJobStatus.Canceled,
            _ => false,
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
