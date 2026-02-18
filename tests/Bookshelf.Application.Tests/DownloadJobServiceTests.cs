using System.Reflection;
using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Application.Services;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Tests;

public class DownloadJobServiceTests
{
    [Fact]
    public async Task ListAsync_NormalizesPagingAndFiltersByStatus()
    {
        var fixture = CreateFixture();
        var completed = CreateQueuedJob(jobId: 100, userId: 10, bookId: 100, externalJobId: "hash-100");
        completed.TransitionTo(DownloadJobStatus.Downloading, DateTimeOffset.UtcNow.AddMinutes(-2));
        completed.TransitionTo(DownloadJobStatus.Completed, DateTimeOffset.UtcNow.AddMinutes(-1));
        fixture.JobRepository.Jobs.Add(completed);

        var failed = CreateQueuedJob(jobId: 101, userId: 10, bookId: 100, externalJobId: "hash-101");
        failed.TransitionTo(DownloadJobStatus.Failed, DateTimeOffset.UtcNow, "provider_error");
        fixture.JobRepository.Jobs.Add(failed);

        var response = await fixture.Service.ListAsync(
            userId: 10,
            status: "completed",
            page: 0,
            pageSize: 1000);

        Assert.Equal(1, response.Page);
        Assert.Equal(20, response.PageSize);
        Assert.Equal(1, response.Total);
        var item = Assert.Single(response.Items);
        Assert.Equal("completed", item.Status);
    }

    [Fact]
    public async Task ListAsync_InvalidStatus_Throws()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await fixture.Service.ListAsync(10, "unsupported", 1, 20));
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenMissingOrWrongUser()
    {
        var fixture = CreateFixture();
        var existing = CreateQueuedJob(jobId: 200, userId: 99, bookId: 100, externalJobId: "hash-200");
        fixture.JobRepository.Jobs.Add(existing);

        var missing = await fixture.Service.GetAsync(jobId: 999, userId: 99);
        var wrongUser = await fixture.Service.GetAsync(jobId: 200, userId: 10);

        Assert.Null(missing);
        Assert.Null(wrongUser);
    }

    [Fact]
    public async Task GetAsync_ActiveJob_SynchronizesBeforeReturning()
    {
        var fixture = CreateFixture();
        var job = CreateQueuedJob(jobId: 201, userId: 10, bookId: 100, externalJobId: "hash-201");
        fixture.JobRepository.Jobs.Add(job);
        fixture.ExecutionClient.StatusByExternalId["hash-201"] = new DownloadStatusResult(
            ExternalDownloadState.Downloading,
            StoragePath: null,
            SizeBytes: null);

        var result = await fixture.Service.GetAsync(201, 10);

        Assert.NotNull(result);
        Assert.Equal("downloading", result!.Status);
    }

    [Fact]
    public async Task SyncActiveAsync_NotFoundWithinGrace_KeepsJobActive()
    {
        var fixture = CreateFixture();
        var job = CreateQueuedJob(jobId: 1, userId: 10, bookId: 100, externalJobId: "hash-1");
        fixture.JobRepository.Jobs.Add(job);
        fixture.ExecutionClient.StatusByExternalId["hash-1"] = new DownloadStatusResult(
            ExternalDownloadState.NotFound,
            StoragePath: null,
            SizeBytes: null);

        await fixture.Service.SyncActiveAsync();

        Assert.Equal(DownloadJobStatus.Queued, job.Status);
        Assert.True(job.FirstNotFoundAtUtc.HasValue);
    }

    [Fact]
    public async Task SyncActiveAsync_NotFoundAfterGrace_MarksFailed()
    {
        var fixture = CreateFixture();
        var job = CreateQueuedJob(jobId: 2, userId: 10, bookId: 100, externalJobId: "hash-2");
        SetProperty(job, "FirstNotFoundAtUtc", DateTimeOffset.UtcNow - TimeSpan.FromSeconds(65));
        fixture.JobRepository.Jobs.Add(job);
        fixture.ExecutionClient.StatusByExternalId["hash-2"] = new DownloadStatusResult(
            ExternalDownloadState.NotFound,
            StoragePath: null,
            SizeBytes: null);

        await fixture.Service.SyncActiveAsync();

        Assert.Equal(DownloadJobStatus.Failed, job.Status);
        Assert.Equal("missing_external_job", job.FailureReason);
    }

    [Fact]
    public async Task SyncActiveAsync_CompletedDownload_UpdatesMediaAndBookState()
    {
        var fixture = CreateFixture();
        var book = new Book("fantlab", "123", "Dune");
        SetId(book, 100);
        var media = book.UpsertMediaAsset(MediaType.Audio, "https://source/item/1", "jackett");
        media.MarkDeleted(MediaAssetStatus.Missing, DateTimeOffset.UtcNow.AddHours(-1));
        book.RecomputeCatalogState();
        fixture.BookRepository.Books.Add(book);

        var job = new DownloadJob(
            userId: 10,
            bookId: 100,
            mediaType: MediaType.Audio,
            source: "https://source/item/1",
            torrentMagnet: "magnet:?xt=urn:btih:hash-3");
        SetId(job, 3);
        job.SetExternalJobId("hash-3", DateTimeOffset.UtcNow);
        job.TransitionTo(DownloadJobStatus.Downloading, DateTimeOffset.UtcNow);
        fixture.JobRepository.Jobs.Add(job);

        fixture.ExecutionClient.StatusByExternalId["hash-3"] = new DownloadStatusResult(
            ExternalDownloadState.Completed,
            StoragePath: "D:\\media\\dune.m4b",
            SizeBytes: 734003200);

        await fixture.Service.SyncActiveAsync();

        Assert.Equal(DownloadJobStatus.Completed, job.Status);
        Assert.Equal(CatalogState.Library, book.CatalogState);
        Assert.Equal(MediaAssetStatus.Available, media.Status);
        Assert.Equal("D:\\media\\dune.m4b", media.StoragePath);
        Assert.Equal("https://source/item/1", media.SourceUrl);
    }

    [Fact]
    public async Task SyncActiveAsync_QueuedWithCompletedExternal_MovesToDownloadingOnly()
    {
        var fixture = CreateFixture();
        var job = CreateQueuedJob(jobId: 4, userId: 10, bookId: 100, externalJobId: "hash-4");
        fixture.JobRepository.Jobs.Add(job);
        fixture.ExecutionClient.StatusByExternalId["hash-4"] = new DownloadStatusResult(
            ExternalDownloadState.Completed,
            StoragePath: "D:\\media\\dune.epub",
            SizeBytes: 1048576);

        await fixture.Service.SyncActiveAsync();

        Assert.Equal(DownloadJobStatus.Downloading, job.Status);
    }

    [Fact]
    public async Task SyncActiveAsync_ExternalFailed_MarksFailedWithProviderError()
    {
        var fixture = CreateFixture();
        var job = CreateQueuedJob(jobId: 41, userId: 10, bookId: 100, externalJobId: "hash-41");
        job.TransitionTo(DownloadJobStatus.Downloading, DateTimeOffset.UtcNow);
        fixture.JobRepository.Jobs.Add(job);
        fixture.ExecutionClient.StatusByExternalId["hash-41"] = new DownloadStatusResult(
            ExternalDownloadState.Failed,
            StoragePath: null,
            SizeBytes: null);

        await fixture.Service.SyncActiveAsync();

        Assert.Equal(DownloadJobStatus.Failed, job.Status);
        Assert.Equal("provider_error", job.FailureReason);
    }

    [Fact]
    public async Task SyncActiveAsync_QueuedState_ClearsNotFoundMarker()
    {
        var fixture = CreateFixture();
        var job = CreateQueuedJob(jobId: 42, userId: 10, bookId: 100, externalJobId: "hash-42");
        SetProperty(job, "FirstNotFoundAtUtc", DateTimeOffset.UtcNow.AddSeconds(-30));
        fixture.JobRepository.Jobs.Add(job);
        fixture.ExecutionClient.StatusByExternalId["hash-42"] = new DownloadStatusResult(
            ExternalDownloadState.Queued,
            StoragePath: null,
            SizeBytes: null);

        await fixture.Service.SyncActiveAsync();

        Assert.Null(job.FirstNotFoundAtUtc);
        Assert.Equal(DownloadJobStatus.Queued, job.Status);
    }

    [Fact]
    public async Task SyncActiveAsync_CompletedWithMissingBook_MarksJobFailed()
    {
        var fixture = CreateFixture();
        var job = CreateQueuedJob(jobId: 43, userId: 10, bookId: 404, externalJobId: "hash-43");
        job.TransitionTo(DownloadJobStatus.Downloading, DateTimeOffset.UtcNow);
        fixture.JobRepository.Jobs.Add(job);
        fixture.ExecutionClient.StatusByExternalId["hash-43"] = new DownloadStatusResult(
            ExternalDownloadState.Completed,
            StoragePath: "D:\\media\\missing-book.epub",
            SizeBytes: 10);

        await fixture.Service.SyncActiveAsync();

        Assert.Equal(DownloadJobStatus.Failed, job.Status);
        Assert.Equal("book_not_found", job.FailureReason);
    }

    [Fact]
    public async Task CancelAsync_ActiveJob_CallsExternalCancelAndMarksCanceled()
    {
        var fixture = CreateFixture();
        var job = new DownloadJob(
            userId: 10,
            bookId: 100,
            mediaType: MediaType.Audio,
            source: "https://source/item/2",
            torrentMagnet: "magnet:?xt=urn:btih:hash-5");
        SetId(job, 5);
        job.SetExternalJobId("hash-5", DateTimeOffset.UtcNow);
        job.TransitionTo(DownloadJobStatus.Downloading, DateTimeOffset.UtcNow);
        fixture.JobRepository.Jobs.Add(job);

        var canceled = await fixture.Service.CancelAsync(5, 10);

        Assert.Equal("canceled", canceled.Status);
        Assert.Equal(DownloadJobStatus.Canceled, job.Status);
        Assert.Single(fixture.ExecutionClient.CancelCalls);
        Assert.Equal("hash-5", fixture.ExecutionClient.CancelCalls[0].ExternalJobId);
        Assert.False(fixture.ExecutionClient.CancelCalls[0].DeleteFiles);
    }

    [Fact]
    public async Task CancelAsync_TerminalJob_Throws()
    {
        var fixture = CreateFixture();
        var job = new DownloadJob(
            userId: 10,
            bookId: 100,
            mediaType: MediaType.Audio,
            source: "https://source/item/2",
            torrentMagnet: "magnet:?xt=urn:btih:hash-6");
        SetId(job, 6);
        job.SetExternalJobId("hash-6", DateTimeOffset.UtcNow);
        job.TransitionTo(DownloadJobStatus.Downloading, DateTimeOffset.UtcNow);
        job.TransitionTo(DownloadJobStatus.Completed, DateTimeOffset.UtcNow);
        fixture.JobRepository.Jobs.Add(job);

        await Assert.ThrowsAsync<DownloadJobCancelNotAllowedException>(
            async () => await fixture.Service.CancelAsync(6, 10));
    }

    private static TestFixture CreateFixture()
    {
        var jobRepository = new FakeDownloadJobRepository();
        var bookRepository = new FakeBookRepository();
        var unitOfWork = new FakeUnitOfWork();
        var executionClient = new FakeDownloadExecutionClient();
        var service = new DownloadJobService(jobRepository, bookRepository, unitOfWork, executionClient);

        return new TestFixture(service, jobRepository, bookRepository, executionClient);
    }

    private static DownloadJob CreateQueuedJob(long jobId, long userId, long bookId, string externalJobId)
    {
        var job = new DownloadJob(
            userId: userId,
            bookId: bookId,
            mediaType: MediaType.Audio,
            source: "https://source/item",
            torrentMagnet: $"magnet:?xt=urn:btih:{externalJobId}");
        SetId(job, jobId);
        job.SetExternalJobId(externalJobId, DateTimeOffset.UtcNow);
        return job;
    }

    private static void SetId<T>(T entity, long id)
    {
        SetProperty(entity, "Id", id);
    }

    private static void SetProperty<T>(T entity, string propertyName, object? value)
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property is null)
        {
            throw new InvalidOperationException($"Property {propertyName} was not found on {typeof(T).Name}.");
        }

        property.SetValue(entity, value);
    }

    private sealed class TestFixture
    {
        public TestFixture(
            IDownloadJobService service,
            FakeDownloadJobRepository jobRepository,
            FakeBookRepository bookRepository,
            FakeDownloadExecutionClient executionClient)
        {
            Service = service;
            JobRepository = jobRepository;
            BookRepository = bookRepository;
            ExecutionClient = executionClient;
        }

        public IDownloadJobService Service { get; }

        public FakeDownloadJobRepository JobRepository { get; }

        public FakeBookRepository BookRepository { get; }

        public FakeDownloadExecutionClient ExecutionClient { get; }
    }

    private sealed class FakeDownloadJobRepository : IDownloadJobRepository
    {
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
                    x.Status is DownloadJobStatus.Queued or DownloadJobStatus.Downloading));
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
            Jobs.Add(job);
            return Task.CompletedTask;
        }

        public void Update(DownloadJob job)
        {
        }
    }

    private sealed class FakeBookRepository : IBookRepository
    {
        public List<Book> Books { get; } = new();

        public Task<Book?> GetByIdAsync(long bookId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Books.SingleOrDefault(x => x.Id == bookId));
        }

        public Task<Book?> GetByProviderKeyAsync(
            string providerCode,
            string providerBookKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Book?>(null);
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
            Books.Add(book);
            return Task.CompletedTask;
        }

        public void Update(Book book)
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
        public Dictionary<string, DownloadStatusResult> StatusByExternalId { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<(string ExternalJobId, bool DeleteFiles)> CancelCalls { get; } = new();

        public TimeSpan NotFoundGracePeriod { get; set; } = TimeSpan.FromSeconds(60);

        public Task<DownloadEnqueueResult> EnqueueAsync(string downloadUri, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DownloadEnqueueResult("hash"));
        }

        public Task<DownloadStatusResult> GetStatusAsync(
            string externalJobId,
            CancellationToken cancellationToken = default)
        {
            if (StatusByExternalId.TryGetValue(externalJobId, out var status))
            {
                return Task.FromResult(status);
            }

            return Task.FromResult(
                new DownloadStatusResult(ExternalDownloadState.Downloading, null, null));
        }

        public Task CancelAsync(
            string externalJobId,
            bool deleteFiles,
            CancellationToken cancellationToken = default)
        {
            CancelCalls.Add((externalJobId, deleteFiles));
            return Task.CompletedTask;
        }
    }
}
