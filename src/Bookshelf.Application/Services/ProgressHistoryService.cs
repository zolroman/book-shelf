using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Services;

public sealed class ProgressHistoryService : IProgressHistoryService
{
    private readonly IProgressSnapshotRepository _progressRepository;
    private readonly IHistoryEventRepository _historyRepository;
    private readonly IBookRepository _bookRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProgressHistoryService(
        IProgressSnapshotRepository progressRepository,
        IHistoryEventRepository historyRepository,
        IBookRepository bookRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _progressRepository = progressRepository;
        _historyRepository = historyRepository;
        _bookRepository = bookRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProgressSnapshotDto> UpsertProgressAsync(
        long userId,
        UpsertProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BookId <= 0)
        {
            throw new ArgumentException("bookId must be greater than zero.", nameof(request.BookId));
        }

        if (string.IsNullOrWhiteSpace(request.PositionRef))
        {
            throw new ArgumentException("positionRef is required.", nameof(request.PositionRef));
        }

        var mediaType = ParseMediaType(request.MediaType);
        var updatedAtUtc = request.UpdatedAtUtc ?? DateTimeOffset.UtcNow;

        await EnsureBookAndUserAsync(userId, request.BookId, cancellationToken);

        var existing = await _progressRepository.GetAsync(
            userId,
            request.BookId,
            mediaType,
            cancellationToken);
        if (existing is null)
        {
            var created = new ProgressSnapshot(
                userId,
                request.BookId,
                mediaType,
                request.PositionRef.Trim(),
                request.ProgressPercent);
            if (created.UpdatedAtUtc != updatedAtUtc)
            {
                created.Update(created.PositionRef, created.ProgressPercent, updatedAtUtc);
            }

            await _progressRepository.AddAsync(created, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Map(created);
        }

        var shouldUpdate = updatedAtUtc > existing.UpdatedAtUtc ||
            (updatedAtUtc == existing.UpdatedAtUtc && request.ProgressPercent > existing.ProgressPercent);
        if (shouldUpdate)
        {
            existing.Update(request.PositionRef.Trim(), request.ProgressPercent, updatedAtUtc);
            _progressRepository.Update(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Map(existing);
    }

    public async Task<ProgressSnapshotsResponse> ListProgressAsync(
        long userId,
        long? bookId,
        string? mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (bookId.HasValue && bookId.Value <= 0)
        {
            throw new ArgumentException("bookId must be greater than zero.", nameof(bookId));
        }

        MediaType? parsedMediaType = string.IsNullOrWhiteSpace(mediaType)
            ? null
            : ParseMediaType(mediaType);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var total = await _progressRepository.CountAsync(
            userId,
            bookId,
            parsedMediaType,
            cancellationToken);
        var items = await _progressRepository.ListAsync(
            userId,
            bookId,
            parsedMediaType,
            safePage,
            safePageSize,
            cancellationToken);

        return new ProgressSnapshotsResponse(
            Page: safePage,
            PageSize: safePageSize,
            Total: total,
            Items: items.Select(Map).ToArray());
    }

    public async Task<AppendHistoryEventsResponse> AppendHistoryAsync(
        long userId,
        AppendHistoryEventsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
        {
            return new AppendHistoryEventsResponse(Added: 0, Deduplicated: 0);
        }

        await _userRepository.EnsureExistsAsync(userId, cancellationToken);

        var added = 0;
        var deduplicated = 0;
        foreach (var item in request.Items)
        {
            if (item.BookId <= 0)
            {
                throw new ArgumentException("bookId must be greater than zero.", nameof(item.BookId));
            }

            var mediaType = ParseMediaType(item.MediaType);
            var eventType = ParseEventType(item.EventType);
            var existingBook = await _bookRepository.GetByIdAsync(item.BookId, cancellationToken);
            if (existingBook is null)
            {
                throw new BookIdNotFoundException(item.BookId);
            }

            var normalizedPositionRef = string.IsNullOrWhiteSpace(item.PositionRef) ? null : item.PositionRef.Trim();
            var eventAtUtc = item.EventAtUtc;
            var exists = await _historyRepository.ExistsAsync(
                userId,
                item.BookId,
                mediaType,
                eventType,
                normalizedPositionRef,
                eventAtUtc,
                cancellationToken);
            if (exists)
            {
                deduplicated++;
                continue;
            }

            var historyEvent = new HistoryEvent(
                userId,
                item.BookId,
                mediaType,
                eventType,
                normalizedPositionRef,
                eventAtUtc);
            await _historyRepository.AddAsync(historyEvent, cancellationToken);
            added++;
        }

        if (added > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new AppendHistoryEventsResponse(Added: added, Deduplicated: deduplicated);
    }

    public async Task<HistoryEventsResponse> ListHistoryAsync(
        long userId,
        long? bookId,
        string? mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (bookId.HasValue && bookId.Value <= 0)
        {
            throw new ArgumentException("bookId must be greater than zero.", nameof(bookId));
        }

        MediaType? parsedMediaType = string.IsNullOrWhiteSpace(mediaType)
            ? null
            : ParseMediaType(mediaType);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var total = await _historyRepository.CountAsync(
            userId,
            bookId,
            parsedMediaType,
            cancellationToken);
        var items = await _historyRepository.ListAsync(
            userId,
            bookId,
            parsedMediaType,
            safePage,
            safePageSize,
            cancellationToken);

        return new HistoryEventsResponse(
            Page: safePage,
            PageSize: safePageSize,
            Total: total,
            Items: items.Select(Map).ToArray());
    }

    private async Task EnsureBookAndUserAsync(long userId, long bookId, CancellationToken cancellationToken)
    {
        var book = await _bookRepository.GetByIdAsync(bookId, cancellationToken);
        if (book is null)
        {
            throw new BookIdNotFoundException(bookId);
        }

        await _userRepository.EnsureExistsAsync(userId, cancellationToken);
    }

    private static MediaType ParseMediaType(string rawMediaType)
    {
        return rawMediaType.Trim().ToLowerInvariant() switch
        {
            "text" => MediaType.Text,
            "audio" => MediaType.Audio,
            _ => throw new ArgumentException("mediaType must be either text or audio.", nameof(rawMediaType)),
        };
    }

    private static HistoryEventType ParseEventType(string rawEventType)
    {
        return rawEventType.Trim().ToLowerInvariant() switch
        {
            "started" => HistoryEventType.Started,
            "progress" => HistoryEventType.Progress,
            "completed" => HistoryEventType.Completed,
            _ => throw new ArgumentException(
                "eventType must be one of started, progress, completed.",
                nameof(rawEventType)),
        };
    }

    private static ProgressSnapshotDto Map(ProgressSnapshot snapshot)
    {
        return new ProgressSnapshotDto(
            UserId: snapshot.UserId,
            BookId: snapshot.BookId,
            MediaType: snapshot.MediaType.ToString().ToLowerInvariant(),
            PositionRef: snapshot.PositionRef,
            ProgressPercent: snapshot.ProgressPercent,
            UpdatedAtUtc: snapshot.UpdatedAtUtc);
    }

    private static HistoryEventDto Map(HistoryEvent historyEvent)
    {
        return new HistoryEventDto(
            Id: historyEvent.Id,
            UserId: historyEvent.UserId,
            BookId: historyEvent.BookId,
            MediaType: historyEvent.MediaType.ToString().ToLowerInvariant(),
            EventType: historyEvent.EventType.ToString().ToLowerInvariant(),
            PositionRef: historyEvent.PositionRef,
            EventAtUtc: historyEvent.EventAtUtc);
    }
}
