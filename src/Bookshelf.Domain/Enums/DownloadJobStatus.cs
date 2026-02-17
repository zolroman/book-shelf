namespace Bookshelf.Domain.Enums;

public enum DownloadJobStatus
{
    Queued = 1,
    Downloading = 2,
    Completed = 3,
    Failed = 4,
    Canceled = 5
}
