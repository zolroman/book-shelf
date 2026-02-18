using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.Repositories;

public sealed class ProgressSnapshotRepository : IProgressSnapshotRepository
{
    private readonly BookshelfDbContext _dbContext;

    public ProgressSnapshotRepository(BookshelfDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ProgressSnapshot?> GetAsync(
        long userId,
        long bookId,
        MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ProgressSnapshots
            .FirstOrDefaultAsync(
                x => x.UserId == userId &&
                     x.BookId == bookId &&
                     x.MediaType == mediaType,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ProgressSnapshot>> ListAsync(
        long userId,
        long? bookId,
        MediaType? mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var query = BuildQuery(userId, bookId, mediaType);

        return await query
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToArrayAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        long userId,
        long? bookId,
        MediaType? mediaType,
        CancellationToken cancellationToken = default)
    {
        return BuildQuery(userId, bookId, mediaType).CountAsync(cancellationToken);
    }

    public Task AddAsync(ProgressSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProgressSnapshots.AddAsync(snapshot, cancellationToken).AsTask();
    }

    public void Update(ProgressSnapshot snapshot)
    {
        _dbContext.ProgressSnapshots.Update(snapshot);
    }

    private IQueryable<ProgressSnapshot> BuildQuery(long userId, long? bookId, MediaType? mediaType)
    {
        var query = _dbContext.ProgressSnapshots
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (bookId.HasValue)
        {
            var requestedBookId = bookId.Value;
            query = query.Where(x => x.BookId == requestedBookId);
        }

        if (mediaType.HasValue)
        {
            var requestedMediaType = mediaType.Value;
            query = query.Where(x => x.MediaType == requestedMediaType);
        }

        return query;
    }
}
