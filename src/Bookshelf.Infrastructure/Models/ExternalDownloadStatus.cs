namespace Bookshelf.Infrastructure.Models;

public enum ExternalDownloadStatus
{
    Unknown = 0,
    Queued = 1,
    Downloading = 2,
    Completed = 3,
    Failed = 4,
    Canceled = 5
}
