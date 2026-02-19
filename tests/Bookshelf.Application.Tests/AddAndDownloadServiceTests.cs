using System.Reflection;
using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Application.Services;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Tests;

public class AddAndDownloadServiceTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesArchiveBookAndStartsDownloadImmediately()
    {
        var details = BuildDetails();
        var candidate = BuildCandidate();
        var fixture = CreateFixture(details, candidate);
        var service = fixture.Service;

        var response = await service.ExecuteAsync(
            new AddAndDownloadRequest(42, "fantlab", "123", "audio", candidate.CandidateId));

        Assert.Equal("downloading", response.DownloadJob.Status);
        Assert.Equal("abc123hash", response.DownloadJob.ExternalJobId);
        _ = Assert.Single(fixture.BookRepository.Books);
        _ = Assert.Single(fixture.JobRepository.Jobs);
        Assert.Contains(42, fixture.UserRepository.EnsuredUserIds);

        var book = fixture.BookRepository.Books.Single();
        Assert.Equal(CatalogState.Archive, book.CatalogState);
        var media = Assert.Single(book.MediaAssets);
        Assert.Equal(MediaType.Audio, media.MediaType);
        Assert.Equal(MediaAssetStatus.Missing, media.Status);
        Assert.Equal(candidate.SourceUrl, media.SourceUrl);

        var job = fixture.JobRepository.Jobs.Single();
        Assert.Equal(DownloadJobStatus.Downloading, job.Status);
        Assert.Equal(candidate.SourceUrl, job.Source);
        Assert.Equal(candidate.DownloadUri, job.TorrentMagnet);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActiveJobExists_ReturnsExistingAndDoesNotEnqueue()
    {
        var details = BuildDetails();
        var candidate = BuildCandidate();
        var fixture = CreateFixture(details, candidate);

        var existingBook = new Book("fantlab", "123", "Dune");
        SetId(existingBook, 100);
        fixture.BookRepository.Books.Add(existingBook);

        var existingJob = new DownloadJob(
            userId: 7,
            bookId: existingBook.Id,
            mediaType: MediaType.Audio,
            source: candidate.SourceUrl,
            torrentMagnet: candidate.DownloadUri);
        SetId(existingJob, 501);
        existingJob.SetExternalJobId("existing-hash", DateTimeOffset.UtcNow);
        existingJob.TransitionTo(DownloadJobStatus.Downloading, DateTimeOffset.UtcNow);
        fixture.JobRepository.Jobs.Add(existingJob);

        var response = await fixture.Service.ExecuteAsync(
            new AddAndDownloadRequest(7, "fantlab", "123", "audio", candidate.CandidateId));

        Assert.Equal(501, response.DownloadJob.Id);
        Assert.Equal("existing-hash", response.DownloadJob.ExternalJobId);
        Assert.Equal(0, fixture.DownloadExecutionClient.EnqueueCalls);
        Assert.Single(fixture.JobRepository.Jobs);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCandidateNotFound_Throws()
    {
        var details = BuildDetails();
        var fixture = CreateFixture(details, candidate: null);

        await Assert.ThrowsAsync<DownloadCandidateNotFoundException>(
            async () => await fixture.Service.ExecuteAsync(
                new AddAndDownloadRequest(3, "fantlab", "123", "audio", "missing")));
    }

    [Fact]
    public async Task ExecuteAsync_WhenEnqueueUnavailable_MarksJobFailed()
    {
        var details = BuildDetails();
        var candidate = BuildCandidate();
        var fixture = CreateFixture(
            details,
            candidate,
            enqueueException: new DownloadExecutionUnavailableException("qbittorrent", "down"));

        await Assert.ThrowsAsync<DownloadExecutionUnavailableException>(
            async () => await fixture.Service.ExecuteAsync(
                new AddAndDownloadRequest(5, "fantlab", "123", "audio", candidate.CandidateId)));

        var job = Assert.Single(fixture.JobRepository.Jobs);
        Assert.Equal(DownloadJobStatus.Failed, job.Status);
        Assert.Equal("enqueue_unavailable", job.FailureReason);
    }

    private static TestFixture CreateFixture(
        SearchBookDetailsResponse details,
        DownloadCandidateDto? candidate,
        Exception? enqueueException = null)
    {
        var fixture = new TestFixture(
            new FakeBookSearchService(details),
            new FakeCandidateDiscoveryService(candidate),
            new FakeBookRepository(),
            new FakeUserRepository(),
            new FakeDownloadJobRepository(),
            new FakeUnitOfWork(),
            new FakeDownloadExecutionClient(enqueueException));

        return fixture;
    }

    private static SearchBookDetailsResponse BuildDetails()
    {
        return new SearchBookDetailsResponse(
            ProviderCode: "fantlab",
            ProviderBookKey: "123",
            Title: "Dune",
            OriginalTitle: "Dune",
            Description: "Sci-fi",
            PublishYear: 1965,
            CoverUrl: "https://images.example/dune.jpg",
            Authors: new[] { "Frank Herbert" },
            Series: new SearchSeriesDto("77", "Dune Saga", 1));
    }

    private static DownloadCandidateDto BuildCandidate()
    {
        return new DownloadCandidateDto(
            CandidateId: "jackett:abc123",
            MediaType: "audio",
            Title: "Dune Audiobook",
            DownloadUri: "magnet:?xt=urn:btih:abc123hash",
            SourceUrl: "https://tracker.example/item/1",
            Seeders: 50,
            SizeBytes: 734003200);
    }

    private static void SetId<T>(T entity, long id)
    {
        var property = typeof(T).GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property is null)
        {
            throw new InvalidOperationException($"Entity {typeof(T).Name} does not have an Id property.");
        }

        property.SetValue(entity, id);
    }

    private sealed class TestFixture
    {
        public TestFixture(
            FakeBookSearchService bookSearchService,
            FakeCandidateDiscoveryService candidateDiscoveryService,
            FakeBookRepository bookRepository,
            FakeUserRepository userRepository,
            FakeDownloadJobRepository downloadJobRepository,
            FakeUnitOfWork unitOfWork,
            FakeDownloadExecutionClient downloadExecutionClient)
        {
            BookRepository = bookRepository;
            UserRepository = userRepository;
            JobRepository = downloadJobRepository;
            DownloadExecutionClient = downloadExecutionClient;
            Service = new AddAndDownloadService(
                bookSearchService,
                candidateDiscoveryService,
                bookRepository,
                userRepository,
                downloadJobRepository,
                unitOfWork,
                downloadExecutionClient);
        }

        public AddAndDownloadService Service { get; }

        public FakeBookRepository BookRepository { get; }

        public FakeUserRepository UserRepository { get; }

        public FakeDownloadJobRepository JobRepository { get; }

        public FakeDownloadExecutionClient DownloadExecutionClient { get; }
    }

    private sealed class FakeBookSearchService : IBookSearchService
    {
        private readonly SearchBookDetailsResponse _details;

        public FakeBookSearchService(SearchBookDetailsResponse details)
        {
            _details = details;
        }

        public Task<SearchBooksResponse> SearchAsync(
            string? title,
            string? author,
            int page,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new SearchBooksResponse(
                    new SearchBooksQuery(title, author),
                    page,
                    25,
                    0,
                    Array.Empty<SearchBookItemDto>()));
        }

        public Task<SearchBookDetailsResponse?> GetDetailsAsync(
            string providerCode,
            string providerBookKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SearchBookDetailsResponse?>(_details);
        }
    }

    private sealed class FakeCandidateDiscoveryService : ICandidateDiscoveryService
    {
        private readonly DownloadCandidateDto? _candidate;

        public FakeCandidateDiscoveryService(DownloadCandidateDto? candidate)
        {
            _candidate = candidate;
        }

        public Task<DownloadCandidatesResponse> FindAsync(
            string providerCode,
            string providerBookKey,
            string mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var items = _candidate is null ? Array.Empty<DownloadCandidateDto>() : new[] { _candidate };
            return Task.FromResult(
                new DownloadCandidatesResponse(
                    providerCode,
                    providerBookKey,
                    mediaType,
                    page,
                    pageSize,
                    items.Length,
                    items));
        }

        public Task<DownloadCandidateDto?> ResolveAsync(
            string providerCode,
            string providerBookKey,
            string mediaType,
            string candidateId,
            CancellationToken cancellationToken = default)
        {
            if (_candidate is not null &&
                _candidate.CandidateId.Equals(candidateId, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<DownloadCandidateDto?>(_candidate);
            }

            return Task.FromResult<DownloadCandidateDto?>(null);
        }
    }

    private sealed class FakeBookRepository : IBookRepository
    {
        private long _nextBookId = 1;
        private long _nextAuthorId = 1;
        private long _nextSeriesId = 1;

        public List<Book> Books { get; } = new();

        public List<Author> Authors { get; } = new();

        public List<Series> SeriesItems { get; } = new();

        public Task<Book?> GetByIdAsync(long bookId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Books.SingleOrDefault(x => x.Id == bookId));
        }

        public Task<Book?> GetByProviderKeyAsync(
            string providerCode,
            string providerBookKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Books.SingleOrDefault(x =>
                    x.ProviderCode == providerCode &&
                    x.ProviderBookKey == providerBookKey));
        }

        public Task<Author?> GetAuthorByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Authors.SingleOrDefault(x => x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        public Task AddAuthorAsync(Author author, CancellationToken cancellationToken = default)
        {
            SetId(author, _nextAuthorId++);
            Authors.Add(author);
            return Task.CompletedTask;
        }

        public Task<Series?> GetSeriesByProviderKeyAsync(
            string providerCode,
            string providerSeriesKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                SeriesItems.SingleOrDefault(x =>
                    x.ProviderCode == providerCode &&
                    x.ProviderSeriesKey == providerSeriesKey));
        }

        public Task AddSeriesAsync(Series series, CancellationToken cancellationToken = default)
        {
            SetId(series, _nextSeriesId++);
            SeriesItems.Add(series);
            return Task.CompletedTask;
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
            IReadOnlyList<Book> items = Books.ToArray();
            return Task.FromResult(items);
        }

        public Task<int> CountLibraryAsync(
            bool includeArchived,
            string? query,
            string? providerCode,
            CatalogState? catalogState,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Books.Count);
        }

        public Task AddAsync(Book book, CancellationToken cancellationToken = default)
        {
            SetId(book, _nextBookId++);
            Books.Add(book);
            return Task.CompletedTask;
        }

        public void Update(Book book)
        {
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public HashSet<long> EnsuredUserIds { get; } = new();

        public Task EnsureExistsAsync(long userId, CancellationToken cancellationToken = default)
        {
            EnsuredUserIds.Add(userId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDownloadJobRepository : IDownloadJobRepository
    {
        private long _nextJobId = 1;

        public List<DownloadJob> Jobs { get; } = new();

        public Task<DownloadJob?> GetByIdAsync(long jobId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Jobs.SingleOrDefault(x => x.Id == jobId));
        }

        public Task<DownloadJob?> GetActiveAsync(
            long userId,
            long bookId,
            MediaType mediaType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Jobs.SingleOrDefault(x =>
                    x.UserId == userId &&
                    x.BookId == bookId &&
                    x.MediaType == mediaType &&
                    (x.Status == DownloadJobStatus.Queued || x.Status == DownloadJobStatus.Downloading)));
        }

        public Task<IReadOnlyList<DownloadJob>> ListByUserAsync(
            long userId,
            DownloadJobStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = Jobs.Where(x => x.UserId == userId);
            if (status.HasValue)
            {
                var statusValue = status.Value;
                query = query.Where(x => x.Status == statusValue);
            }

            IReadOnlyList<DownloadJob> items = query
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToArray();
            return Task.FromResult(items);
        }

        public Task<int> CountByUserAsync(
            long userId,
            DownloadJobStatus? status,
            CancellationToken cancellationToken = default)
        {
            var query = Jobs.Where(x => x.UserId == userId);
            if (status.HasValue)
            {
                var statusValue = status.Value;
                query = query.Where(x => x.Status == statusValue);
            }

            return Task.FromResult(query.Count());
        }

        public Task<IReadOnlyList<DownloadJob>> ListActiveAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DownloadJob> items = Jobs
                .Where(x => x.Status is DownloadJobStatus.Queued or DownloadJobStatus.Downloading)
                .Take(limit)
                .ToArray();
            return Task.FromResult(items);
        }

        public Task AddAsync(DownloadJob job, CancellationToken cancellationToken = default)
        {
            SetId(job, _nextJobId++);
            Jobs.Add(job);
            return Task.CompletedTask;
        }

        public void Update(DownloadJob job)
        {
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1);
        }
    }

    private sealed class FakeDownloadExecutionClient : IDownloadExecutionClient
    {
        private readonly Exception? _enqueueException;

        public FakeDownloadExecutionClient(Exception? enqueueException)
        {
            _enqueueException = enqueueException;
        }

        public int EnqueueCalls { get; private set; }

        public TimeSpan NotFoundGracePeriod => TimeSpan.FromMinutes(1);

        public Task<DownloadEnqueueResult> EnqueueAsync(
            string downloadUri,
            CancellationToken cancellationToken = default)
        {
            EnqueueCalls++;
            if (_enqueueException is not null)
            {
                throw _enqueueException;
            }

            return Task.FromResult(new DownloadEnqueueResult("abc123hash"));
        }

        public Task<DownloadStatusResult> GetStatusAsync(
            string externalJobId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new DownloadStatusResult(
                    ExternalDownloadState.Downloading,
                    StoragePath: null,
                    SizeBytes: null));
        }

        public Task CancelAsync(
            string externalJobId,
            bool deleteFiles,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
