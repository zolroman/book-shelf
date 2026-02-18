namespace Bookshelf.Application.Abstractions.Providers;

public interface IDownloadExecutionClient
{
    Task<DownloadEnqueueResult> EnqueueAsync(
        string downloadUri,
        CancellationToken cancellationToken = default);
}

public sealed record DownloadEnqueueResult(string? ExternalJobId);
