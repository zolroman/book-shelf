namespace Bookshelf.Application.Exceptions;

public sealed class DownloadJobNotFoundException : Exception
{
    public DownloadJobNotFoundException(long jobId)
        : base($"Download job '{jobId}' was not found.")
    {
        JobId = jobId;
    }

    public long JobId { get; }
}
