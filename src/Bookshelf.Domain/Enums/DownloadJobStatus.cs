namespace Bookshelf.Domain.Enums;

public enum DownloadJobStatus
{
    Queued = 0,
    Downloading = 1,
    Completed = 2,
    Failed = 3,
    Canceled = 4,
}
