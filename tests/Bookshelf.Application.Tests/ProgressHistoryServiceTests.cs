using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Application.Services;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Tests;

public class ProgressHistoryServiceTests
{
    [Fact]
    public async Task UpsertProgress_OlderUpdate_DoesNotOverrideNewerSnapshot()
    {
        var dependencies = CreateDependencies();
        var baseline = DateTimeOffset.UtcNow;
        var existing = new ProgressSnapshot(1, 10, MediaType.Text, "p20", 20m);
        existing.Update("p80", 80m, baseline);
        dependencies.ProgressRepository.Seed(existing);

        IProgressHistoryService service = CreateService(dependencies);

        var result = await service.UpsertProgressAsync(
            1,
            new UpsertProgressRequest(
                BookId: 10,
                MediaType: "text",
                PositionRef: "p40",
                ProgressPercent: 40m,
                UpdatedAtUtc: baseline.AddMinutes(-10)));

        Assert.Equal("p80", result.PositionRef);
        Assert.Equal(80m, result.ProgressPercent);
    }

    [Fact]
    public async Task UpsertProgress_SameTimestamp_HigherProgressWins()
    {
        var dependencies = CreateDependencies();
        var baseline = DateTimeOffset.UtcNow;
        var existing = new ProgressSnapshot(1, 10, MediaType.Audio, "a30", 30m);
        existing.Update("a30", 30m, baseline);
        dependencies.ProgressRepository.Seed(existing);

        IProgressHistoryService service = CreateService(dependencies);

        var result = await service.UpsertProgressAsync(
            1,
            new UpsertProgressRequest(
                BookId: 10,
                MediaType: "audio",
                PositionRef: "a60",
                ProgressPercent: 60m,
                UpdatedAtUtc: baseline));

        Assert.Equal("a60", result.PositionRef);
        Assert.Equal(60m, result.ProgressPercent);
    }

    [Fact]
    public async Task AppendHistory_DuplicatePayload_IsDeduplicated()
    {
        var dependencies = CreateDependencies();
        IProgressHistoryService service = CreateService(dependencies);
        var eventAtUtc = DateTimeOffset.UtcNow;
        var payload = new AppendHistoryEventsRequest(
            Items:
            [
                new HistoryEventWriteDto(
                    BookId: 10,
                    MediaType: "text",
                    EventType: "progress",
                    PositionRef: "p42",
                    EventAtUtc: eventAtUtc),
                new HistoryEventWriteDto(
                    BookId: 10,
                    MediaType: "text",
                    EventType: "progress",
                    PositionRef: "p42",
                    EventAtUtc: eventAtUtc),
            ]);

        var response = await service.AppendHistoryAsync(1, payload);

        Assert.Equal(1, response.Added);
        Assert.Equal(1, response.Deduplicated);
    }

    [Fact]
    public async Task UpsertProgress_MissingBook_ThrowsNotFound()
    {
        var dependencies = CreateDependencies();
        dependencies.BookRepository.RemoveBook(10);
        IProgressHistoryService service = CreateService(dependencies);

        await Assert.ThrowsAsync<BookIdNotFoundException>(
            async () => await service.UpsertProgressAsync(
                1,
                new UpsertProgressRequest(
                    BookId: 10,
                    MediaType: "text",
                    PositionRef: "p10",
                    ProgressPercent: 10m,
                UpdatedAtUtc: DateTimeOffset.UtcNow)));
    }

    [Fact]
    public async Task ListProgress_AppliesPagingAndFilters()
    {
        var dependencies = CreateDependencies();
        IProgressHistoryService service = CreateService(dependencies);

        await service.UpsertProgressAsync(
            1,
            new UpsertProgressRequest(10, "text", "p10", 10m, DateTimeOffset.UtcNow.AddMinutes(-2)));
        await service.UpsertProgressAsync(
            1,
            new UpsertProgressRequest(10, "audio", "a10", 10m, DateTimeOffset.UtcNow.AddMinutes(-1)));

        var response = await service.ListProgressAsync(
            userId: 1,
            bookId: 10,
            mediaType: "text",
            page: 0,
            pageSize: 1000);

        Assert.Equal(1, response.Page);
        Assert.Equal(20, response.PageSize);
        Assert.Equal(1, response.Total);
        var item = Assert.Single(response.Items);
        Assert.Equal("text", item.MediaType);
    }

    [Fact]
    public async Task ListProgress_InvalidFilter_Throws()
    {
        var dependencies = CreateDependencies();
        IProgressHistoryService service = CreateService(dependencies);

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await service.ListProgressAsync(1, 0, null, 1, 20));

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await service.ListProgressAsync(1, 10, "video", 1, 20));
    }

    [Fact]
    public async Task AppendHistory_EmptyBatch_ReturnsZeroWithoutSaving()
    {
        var dependencies = CreateDependencies();
        IProgressHistoryService service = CreateService(dependencies);

        var result = await service.AppendHistoryAsync(1, new AppendHistoryEventsRequest(Array.Empty<HistoryEventWriteDto>()));

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Deduplicated);
        Assert.Equal(0, dependencies.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ListHistory_AppliesPagingAndFilters()
    {
        var dependencies = CreateDependencies();
        IProgressHistoryService service = CreateService(dependencies);

        await service.AppendHistoryAsync(
            1,
            new AppendHistoryEventsRequest(
                [
                    new HistoryEventWriteDto(10, "text", "started", "p1", DateTimeOffset.UtcNow.AddMinutes(-2)),
                    new HistoryEventWriteDto(10, "audio", "progress", "a1", DateTimeOffset.UtcNow.AddMinutes(-1)),
                ]));

        var response = await service.ListHistoryAsync(
            userId: 1,
            bookId: 10,
            mediaType: "audio",
            page: 0,
            pageSize: 1000);

        Assert.Equal(1, response.Page);
        Assert.Equal(20, response.PageSize);
        Assert.Equal(1, response.Total);
        var item = Assert.Single(response.Items);
        Assert.Equal("audio", item.MediaType);
    }

    [Fact]
    public async Task ListHistory_InvalidFilter_Throws()
    {
        var dependencies = CreateDependencies();
        IProgressHistoryService service = CreateService(dependencies);

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await service.ListHistoryAsync(1, 0, null, 1, 20));

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await service.ListHistoryAsync(1, 10, "video", 1, 20));
    }

    private static IProgressHistoryService CreateService(TestDependencies dependencies)
    {
        return new ProgressHistoryService(
            dependencies.ProgressRepository,
            dependencies.HistoryRepository,
            dependencies.BookRepository,
            dependencies.UserRepository,
            dependencies.UnitOfWork);
    }

    private static TestDependencies CreateDependencies()
    {
        return new TestDependencies();
    }

    private sealed class TestDependencies
    {
        public TestDependencies()
        {
            BookRepository = new FakeBookRepository();
            UserRepository = new FakeUserRepository();
            ProgressRepository = new FakeProgressSnapshotRepository();
            HistoryRepository = new FakeHistoryEventRepository();
            UnitOfWork = new FakeUnitOfWork();
            BookRepository.AddBook(CreateBook(10, "Dune"));
        }

        public FakeBookRepository BookRepository { get; }

        public FakeUserRepository UserRepository { get; }

        public FakeProgressSnapshotRepository ProgressRepository { get; }

        public FakeHistoryEventRepository HistoryRepository { get; }

        public FakeUnitOfWork UnitOfWork { get; }
    }

    private static Book CreateBook(long id, string title)
    {
        var book = new Book("fantlab", id.ToString(), title);
        SetProperty(book, nameof(Book.Id), id);
        return book;
    }

    private static void SetProperty<T>(T entity, string propertyName, object? value)
    {
        var property = typeof(T).GetProperty(
            propertyName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        if (property is null)
        {
            throw new InvalidOperationException($"Property {propertyName} was not found.");
        }

        property.SetValue(entity, value);
    }

    private sealed class FakeBookRepository : IBookRepository
    {
        private readonly Dictionary<long, Book> _booksById = [];

        public void AddBook(Book book)
        {
            _booksById[book.Id] = book;
        }

        public void RemoveBook(long bookId)
        {
            _booksById.Remove(bookId);
        }

        public Task<Book?> GetByIdAsync(long bookId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_booksById.GetValueOrDefault(bookId));
        }

        public Task<Book?> GetByProviderKeyAsync(
            string providerCode,
            string providerBookKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _booksById.Values.SingleOrDefault(x =>
                    x.ProviderCode == providerCode &&
                    x.ProviderBookKey == providerBookKey));
        }

        public Task<Author?> GetAuthorByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Author?>(null);
        }

        public Task AddAuthorAsync(Author author, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Series?> GetSeriesByProviderKeyAsync(
            string providerCode,
            string providerSeriesKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Series?>(null);
        }

        public Task AddSeriesAsync(Series series, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Book>> ListLibraryAsync(
            bool includeArchived,
            string? query,
            string? providerCode,
            CatalogState? catalogState,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Book>>(Array.Empty<Book>());
        }

        public Task<int> CountLibraryAsync(
            bool includeArchived,
            string? query,
            string? providerCode,
            CatalogState? catalogState,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task AddAsync(Book book, CancellationToken cancellationToken = default)
        {
            _booksById[book.Id] = book;
            return Task.CompletedTask;
        }

        public void Update(Book book)
        {
            _booksById[book.Id] = book;
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public Task EnsureExistsAsync(long userId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProgressSnapshotRepository : IProgressSnapshotRepository
    {
        private readonly Dictionary<(long UserId, long BookId, MediaType MediaType), ProgressSnapshot> _storage = [];

        public void Seed(ProgressSnapshot snapshot)
        {
            _storage[(snapshot.UserId, snapshot.BookId, snapshot.MediaType)] = snapshot;
        }

        public Task<ProgressSnapshot?> GetAsync(
            long userId,
            long bookId,
            MediaType mediaType,
            CancellationToken cancellationToken = default)
        {
            _storage.TryGetValue((userId, bookId, mediaType), out var snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<IReadOnlyList<ProgressSnapshot>> ListAsync(
            long userId,
            long? bookId,
            MediaType? mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _storage.Values.Where(x => x.UserId == userId);
            if (bookId.HasValue)
            {
                query = query.Where(x => x.BookId == bookId.Value);
            }

            if (mediaType.HasValue)
            {
                query = query.Where(x => x.MediaType == mediaType.Value);
            }

            return Task.FromResult<IReadOnlyList<ProgressSnapshot>>(
                query
                    .OrderByDescending(x => x.UpdatedAtUtc)
                    .ToArray());
        }

        public Task<int> CountAsync(
            long userId,
            long? bookId,
            MediaType? mediaType,
            CancellationToken cancellationToken = default)
        {
            var query = _storage.Values.Where(x => x.UserId == userId);
            if (bookId.HasValue)
            {
                query = query.Where(x => x.BookId == bookId.Value);
            }

            if (mediaType.HasValue)
            {
                query = query.Where(x => x.MediaType == mediaType.Value);
            }

            return Task.FromResult(query.Count());
        }

        public Task AddAsync(ProgressSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            _storage[(snapshot.UserId, snapshot.BookId, snapshot.MediaType)] = snapshot;
            return Task.CompletedTask;
        }

        public void Update(ProgressSnapshot snapshot)
        {
            _storage[(snapshot.UserId, snapshot.BookId, snapshot.MediaType)] = snapshot;
        }
    }

    private sealed class FakeHistoryEventRepository : IHistoryEventRepository
    {
        private readonly List<HistoryEvent> _events = [];
        private long _nextId = 1;

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
            var exists = _events.Any(x =>
                x.UserId == userId &&
                x.BookId == bookId &&
                x.MediaType == mediaType &&
                x.EventType == eventType &&
                x.PositionRef == normalizedPositionRef &&
                x.EventAtUtc == eventAtUtc);

            return Task.FromResult(exists);
        }

        public Task AddAsync(HistoryEvent historyEvent, CancellationToken cancellationToken = default)
        {
            SetProperty(historyEvent, nameof(HistoryEvent.Id), _nextId++);
            _events.Add(historyEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<HistoryEvent>> ListAsync(
            long userId,
            long? bookId,
            MediaType? mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _events.Where(x => x.UserId == userId);
            if (bookId.HasValue)
            {
                query = query.Where(x => x.BookId == bookId.Value);
            }

            if (mediaType.HasValue)
            {
                query = query.Where(x => x.MediaType == mediaType.Value);
            }

            return Task.FromResult<IReadOnlyList<HistoryEvent>>(
                query
                    .OrderByDescending(x => x.EventAtUtc)
                    .ThenByDescending(x => x.Id)
                    .ToArray());
        }

        public Task<int> CountAsync(
            long userId,
            long? bookId,
            MediaType? mediaType,
            CancellationToken cancellationToken = default)
        {
            var query = _events.Where(x => x.UserId == userId);
            if (bookId.HasValue)
            {
                query = query.Where(x => x.BookId == bookId.Value);
            }

            if (mediaType.HasValue)
            {
                query = query.Where(x => x.MediaType == mediaType.Value);
            }

            return Task.FromResult(query.Count());
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }
}
