using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Abstractions.Persistence;

public interface IDownloadJobRepository
{
    Task<DownloadJob?> GetByIdAsync(long jobId, CancellationToken cancellationToken = default);

    Task<DownloadJob?> GetActiveAsync(
        long userId,
        long bookId,
        MediaType mediaType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DownloadJob>> ListByUserAsync(
        long userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(DownloadJob job, CancellationToken cancellationToken = default);

    void Update(DownloadJob job);
}
