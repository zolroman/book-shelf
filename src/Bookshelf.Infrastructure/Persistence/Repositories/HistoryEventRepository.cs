using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.Repositories;

public sealed class HistoryEventRepository : IHistoryEventRepository
{
    private readonly BookshelfDbContext _dbContext;

    public HistoryEventRepository(BookshelfDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(
        long userId,
        long bookId,
        MediaType mediaType,
        HistoryEventType eventType,
        string? positionRef,
        DateTimeOffset eventAtUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedPositionRef = string.IsNullOrWhiteSpace(positionRef) ? null : positionRef.Trim();
        return _dbContext.HistoryEvents.AnyAsync(
            x => x.UserId == userId &&
                 x.BookId == bookId &&
                 x.MediaType == mediaType &&
                 x.EventType == eventType &&
                 x.PositionRef == normalizedPositionRef &&
                 x.EventAtUtc == eventAtUtc,
            cancellationToken);
    }

    public Task AddAsync(HistoryEvent historyEvent, CancellationToken cancellationToken = default)
    {
        return _dbContext.HistoryEvents.AddAsync(historyEvent, cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<HistoryEvent>> ListAsync(
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
            .OrderByDescending(x => x.EventAtUtc)
            .ThenByDescending(x => x.Id)
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

    private IQueryable<HistoryEvent> BuildQuery(long userId, long? bookId, MediaType? mediaType)
    {
        var query = _dbContext.HistoryEvents
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
