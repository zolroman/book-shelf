using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.Repositories;

public sealed class DownloadJobRepository : IDownloadJobRepository
{
    private readonly BookshelfDbContext _dbContext;

    public DownloadJobRepository(BookshelfDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DownloadJob?> GetByIdAsync(long jobId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DownloadJobs
            .FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
    }

    public async Task<DownloadJob?> GetActiveAsync(
        long userId,
        long bookId,
        MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DownloadJobs
            .FirstOrDefaultAsync(
                x => x.UserId == userId
                    && x.BookId == bookId
                    && x.MediaType == mediaType
                    && (x.Status == DownloadJobStatus.Queued || x.Status == DownloadJobStatus.Downloading),
                cancellationToken);
    }

    public async Task<IReadOnlyList<DownloadJob>> ListByUserAsync(
        long userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : pageSize;

        return await _dbContext.DownloadJobs
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DownloadJob job, CancellationToken cancellationToken = default)
    {
        await _dbContext.DownloadJobs.AddAsync(job, cancellationToken);
    }

    public void Update(DownloadJob job)
    {
        _dbContext.DownloadJobs.Update(job);
    }
}
