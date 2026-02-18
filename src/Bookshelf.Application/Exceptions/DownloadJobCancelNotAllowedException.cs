namespace Bookshelf.Application.Exceptions;

public sealed class DownloadJobCancelNotAllowedException : Exception
{
    public DownloadJobCancelNotAllowedException(long jobId, string status)
        : base($"Download job '{jobId}' cannot be canceled from status '{status}'.")
    {
        JobId = jobId;
        Status = status;
    }

    public long JobId { get; }

    public string Status { get; }
}
