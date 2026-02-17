using Bookshelf.Domain.Entities;

namespace Bookshelf.Infrastructure.Services;

public interface IDownloadService
{
    Task<IReadOnlyList<DownloadJob>> GetJobsAsync(int userId, CancellationToken cancellationToken);

    Task<DownloadJob?> GetJobAsync(int jobId, CancellationToken cancellationToken);

    Task<DownloadJob> StartAsync(int userId, int bookFormatId, string source, CancellationToken cancellationToken);

    Task<DownloadJob?> CancelAsync(int jobId, CancellationToken cancellationToken);
}
