using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Entities;

public class DownloadJob
{
    private static readonly IReadOnlyDictionary<DownloadJobStatus, DownloadJobStatus[]> AllowedTransitions =
        new Dictionary<DownloadJobStatus, DownloadJobStatus[]>
        {
            [DownloadJobStatus.Queued] = [DownloadJobStatus.Downloading, DownloadJobStatus.Canceled, DownloadJobStatus.Failed],
            [DownloadJobStatus.Downloading] = [DownloadJobStatus.Completed, DownloadJobStatus.Canceled, DownloadJobStatus.Failed],
            [DownloadJobStatus.Completed] = [],
            [DownloadJobStatus.Canceled] = [],
            [DownloadJobStatus.Failed] = []
        };

    public int Id { get; set; }

    public int UserId { get; set; }

    public int BookFormatId { get; set; }

    public DownloadJobStatus Status { get; private set; } = DownloadJobStatus.Queued;

    public string Source { get; set; } = string.Empty;

    public string ExternalJobId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public void TransitionTo(DownloadJobStatus newStatus, DateTime utcNow)
    {
        if (!AllowedTransitions[Status].Contains(newStatus))
        {
            throw new InvalidOperationException($"Invalid status transition: {Status} -> {newStatus}");
        }

        Status = newStatus;
        if (newStatus is DownloadJobStatus.Completed or DownloadJobStatus.Canceled or DownloadJobStatus.Failed)
        {
            CompletedAtUtc = utcNow;
        }
    }
}
