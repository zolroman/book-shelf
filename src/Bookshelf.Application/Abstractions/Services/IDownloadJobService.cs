using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Abstractions.Services;

public interface IDownloadJobService
{
    Task<DownloadJobsResponse> ListAsync(
        long userId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<DownloadJobDto?> GetAsync(
        long jobId,
        long userId,
        CancellationToken cancellationToken = default);

    Task<DownloadJobDto> CancelAsync(
        long jobId,
        long userId,
        CancellationToken cancellationToken = default);

    Task SyncActiveAsync(CancellationToken cancellationToken = default);
}
