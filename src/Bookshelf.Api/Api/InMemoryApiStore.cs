using System.Collections.Concurrent;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Api.Api;

public sealed class InMemoryApiStore
{
    private long _nextBookId = 100;
    private long _nextJobId = 1000;
    private long _nextShelfId = 1;
    private readonly ConcurrentDictionary<long, DownloadJobDto> _jobs = new();
    private readonly ConcurrentDictionary<long, ShelfState> _shelves = new();
    private readonly object _shelfLock = new();

    public AddAndDownloadResponse CreateDownloadJob(AddAndDownloadRequest request)
    {
        var bookId = Interlocked.Increment(ref _nextBookId);
        var jobId = Interlocked.Increment(ref _nextJobId);
        var createdAtUtc = DateTimeOffset.UtcNow;

        var job = new DownloadJobDto(
            Id: jobId,
            UserId: request.UserId,
            BookId: bookId,
            MediaType: request.MediaType,
            Status: "downloading",
            ExternalJobId: null,
            FailureReason: null,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: createdAtUtc,
            CompletedAtUtc: null);

        _jobs[jobId] = job;

        return new AddAndDownloadResponse(
            BookId: bookId,
            BookState: "archive",
            DownloadJob: new DownloadJobSummaryDto(
                Id: jobId,
                Status: "downloading",
                ExternalJobId: null,
                CreatedAtUtc: createdAtUtc));
    }

    public DownloadJobsResponse ListJobs(long userId, string? status, int page, int pageSize)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var items = _jobs.Values
            .Where(x => x.UserId == userId)
            .Where(x => string.IsNullOrWhiteSpace(status) || x.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();

        var pageItems = items.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToArray();

        return new DownloadJobsResponse(
            Page: safePage,
            PageSize: safePageSize,
            Total: items.Count,
            Items: pageItems);
    }

    public DownloadJobDto? GetJob(long jobId, long userId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return null;
        }

        return job.UserId == userId ? job : null;
    }

    public DownloadJobDto? CancelJob(long jobId, long userId)
    {
        if (!_jobs.TryGetValue(jobId, out var existing) || existing.UserId != userId)
        {
            return null;
        }

        if (existing.Status is "completed" or "failed" or "canceled")
        {
            return existing;
        }

        var canceled = existing with
        {
            Status = "canceled",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        _jobs[jobId] = canceled;
        return canceled;
    }

    public ShelvesResponse ListShelves(long userId)
    {
        var shelves = _shelves.Values
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Name)
            .Select(ToDto)
            .ToArray();

        return new ShelvesResponse(shelves);
    }

    public ShelfDto? CreateShelf(long userId, string name)
    {
        lock (_shelfLock)
        {
            var exists = _shelves.Values.Any(x =>
                x.UserId == userId &&
                x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (exists)
            {
                return null;
            }

            var state = new ShelfState(
                id: Interlocked.Increment(ref _nextShelfId),
                userId: userId,
                name: name,
                createdAtUtc: DateTimeOffset.UtcNow);

            _shelves[state.Id] = state;
            return ToDto(state);
        }
    }

    public ShelfMutationResult AddBookToShelf(long shelfId, long userId, long bookId)
    {
        if (!_shelves.TryGetValue(shelfId, out var shelf) || shelf.UserId != userId)
        {
            return ShelfMutationResult.NotFound;
        }

        lock (shelf.Lock)
        {
            if (!shelf.BookIds.Add(bookId))
            {
                return ShelfMutationResult.AlreadyExists;
            }
        }

        return ShelfMutationResult.Success(ToDto(shelf));
    }

    public bool RemoveBookFromShelf(long shelfId, long userId, long bookId)
    {
        if (!_shelves.TryGetValue(shelfId, out var shelf) || shelf.UserId != userId)
        {
            return false;
        }

        lock (shelf.Lock)
        {
            shelf.BookIds.Remove(bookId);
        }

        return true;
    }

    private static ShelfDto ToDto(ShelfState state)
    {
        lock (state.Lock)
        {
            return new ShelfDto(
                Id: state.Id,
                UserId: state.UserId,
                Name: state.Name,
                CreatedAtUtc: state.CreatedAtUtc,
                BookIds: state.BookIds.OrderBy(x => x).ToArray());
        }
    }

    private sealed class ShelfState
    {
        public ShelfState(long id, long userId, string name, DateTimeOffset createdAtUtc)
        {
            Id = id;
            UserId = userId;
            Name = name;
            CreatedAtUtc = createdAtUtc;
        }

        public long Id { get; }
        public long UserId { get; }
        public string Name { get; }
        public DateTimeOffset CreatedAtUtc { get; }
        public HashSet<long> BookIds { get; } = new();
        public object Lock { get; } = new();
    }
}

public readonly record struct ShelfMutationResult(bool IsNotFound, bool IsAlreadyExists, ShelfDto? Shelf)
{
    public static ShelfMutationResult NotFound => new(IsNotFound: true, IsAlreadyExists: false, Shelf: null);
    public static ShelfMutationResult AlreadyExists => new(IsNotFound: false, IsAlreadyExists: true, Shelf: null);
    public static ShelfMutationResult Success(ShelfDto shelf) => new(IsNotFound: false, IsAlreadyExists: false, Shelf: shelf);
}
