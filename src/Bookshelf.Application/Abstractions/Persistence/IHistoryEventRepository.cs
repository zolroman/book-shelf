using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Abstractions.Persistence;

public interface IHistoryEventRepository
{
    Task<bool> ExistsAsync(
        long userId,
        long bookId,
        MediaType mediaType,
        HistoryEventType eventType,
        string? positionRef,
        DateTimeOffset eventAtUtc,
        CancellationToken cancellationToken = default);

    Task AddAsync(HistoryEvent historyEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistoryEvent>> ListAsync(
        long userId,
        long? bookId,
        MediaType? mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        long userId,
        long? bookId,
        MediaType? mediaType,
        CancellationToken cancellationToken = default);
}
