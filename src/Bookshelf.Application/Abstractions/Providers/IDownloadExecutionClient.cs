namespace Bookshelf.Application.Abstractions.Providers;

public interface IDownloadExecutionClient
{
    TimeSpan NotFoundGracePeriod { get; }

    Task<DownloadEnqueueResult> EnqueueAsync(
        string downloadUri,
        string candidateId,
        CancellationToken cancellationToken = default);

    Task<DownloadStatusResult> GetStatusAsync(
        string externalJobId,
        CancellationToken cancellationToken = default);

    Task CancelAsync(
        string externalJobId,
        bool deleteFiles,
        CancellationToken cancellationToken = default);
}

public sealed record DownloadEnqueueResult(string? ExternalJobId);

public sealed record DownloadStatusResult(
    ExternalDownloadState State,
    string? StoragePath,
    long? SizeBytes);

public enum ExternalDownloadState
{
    Queued = 0,
    Downloading = 1,
    Completed = 2,
    Failed = 3,
    NotFound = 4,
}
