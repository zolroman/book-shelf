using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Application.Services;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Tests;

public class LibraryServiceTests
{
    [Fact]
    public async Task ListAsync_MapsBooksAndUsesDefaultPaging()
    {
        var repository = new FakeBookRepository
        {
            Books =
            [
                CreateBook(1, CatalogState.Library, "Dune"),
                CreateBook(2, CatalogState.Archive, "Dune Messiah"),
            ],
        };

        ILibraryService service = CreateService(repository);

        var response = await service.ListAsync(
            userId: 10,
            includeArchived: true,
            query: null,
            providerCode: null,
            catalogState: null,
            page: 0,
            pageSize: 0);

        Assert.Equal(1, response.Page);
        Assert.Equal(20, response.PageSize);
        Assert.Equal(2, response.Total);
        Assert.Equal(2, response.Items.Count);
        Assert.Contains(response.Items, item => item.CatalogState == "library");
        var dune = Assert.Single(response.Items, item => item.Id == 1);
        Assert.True(dune.HasTextMedia);
        Assert.False(dune.HasAudioMedia);
    }

    [Fact]
    public async Task ListAsync_InvalidCatalogState_Throws()
    {
        var repository = new FakeBookRepository();
        ILibraryService service = CreateService(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await service.ListAsync(
                userId: 10,
                includeArchived: true,
                query: null,
                providerCode: null,
                catalogState: "invalid",
                page: 1,
                pageSize: 20));
    }

    [Fact]
    public async Task ArchiveAsync_RemovesMedia_CancelsActiveJobs_AndMarksBookArchived()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"bookshelf-library-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);

        try
        {
            var textPath = Path.Combine(rootPath, "dune.epub");
            await File.WriteAllTextAsync(textPath, "content");

            var audioPath = Path.Combine(rootPath, "dune-audio");
            Directory.CreateDirectory(audioPath);
            await File.WriteAllTextAsync(Path.Combine(audioPath, "chapter-01.mp3"), "content");

            var book = new Book("fantlab", "42", "Dune");
            SetProperty(book, "Id", 42L);

            var textAsset = book.UpsertMediaAsset(MediaType.Text, "https://tracker.example/dune-text", "jackett");
            textAsset.MarkAvailable(textPath, 128, checksum: null, completedAtUtc: DateTimeOffset.UtcNow.AddDays(-2));

            var audioAsset = book.UpsertMediaAsset(MediaType.Audio, "https://tracker.example/dune-audio", "jackett");
            audioAsset.MarkAvailable(audioPath, 256, checksum: null, completedAtUtc: DateTimeOffset.UtcNow.AddDays(-1));

            book.RecomputeCatalogState();

            var repository = new FakeBookRepository { Books = [book] };
            var jobRepository = new FakeDownloadJobRepository();
            var queuedJob = CreateJob(1, userId: 10, bookId: 42, MediaType.Text, DownloadJobStatus.Queued, "torrent-queued");
            var downloadingJob = CreateJob(2, userId: 10, bookId: 42, MediaType.Audio, DownloadJobStatus.Downloading, "torrent-downloading");
            var completedJob = CreateJob(3, userId: 10, bookId: 42, MediaType.Audio, DownloadJobStatus.Completed, "torrent-completed");
            var failedJob = CreateJob(4, userId: 10, bookId: 42, MediaType.Text, DownloadJobStatus.Failed, null);
            jobRepository.Jobs.AddRange([queuedJob, downloadingJob, completedJob, failedJob]);

            var unitOfWork = new FakeUnitOfWork();
            var executionClient = new FakeDownloadExecutionClient();
            ILibraryService service = CreateService(repository, jobRepository, unitOfWork, executionClient);

            await service.ArchiveAsync(userId: 10, bookId: 42);

            Assert.False(File.Exists(textPath));
            Assert.False(Directory.Exists(audioPath));
            Assert.All(book.MediaAssets, asset =>
            {
                Assert.Equal(MediaAssetStatus.Deleted, asset.Status);
                Assert.Null(asset.StoragePath);
            });
            Assert.Equal(CatalogState.Archive, book.CatalogState);

            Assert.Equal(DownloadJobStatus.Canceled, queuedJob.Status);
            Assert.Equal(DownloadJobStatus.Canceled, downloadingJob.Status);
            Assert.Equal(DownloadJobStatus.Completed, completedJob.Status);
            Assert.Equal(DownloadJobStatus.Failed, failedJob.Status);

            Assert.Equal(3, executionClient.CancelCalls.Count);
            Assert.All(executionClient.CancelCalls, call => Assert.True(call.DeleteFiles));
            Assert.Contains(executionClient.CancelCalls, call => call.ExternalJobId == "torrent-queued");
            Assert.Contains(executionClient.CancelCalls, call => call.ExternalJobId == "torrent-downloading");
            Assert.Contains(executionClient.CancelCalls, call => call.ExternalJobId == "torrent-completed");
            Assert.Equal(1, unitOfWork.SaveChangesCalls);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ArchiveAsync_BookNotFound_Throws()
    {
        var repository = new FakeBookRepository();
        var jobRepository = new FakeDownloadJobRepository();
        var unitOfWork = new FakeUnitOfWork();
        var executionClient = new FakeDownloadExecutionClient();
        ILibraryService service = CreateService(repository, jobRepository, unitOfWork, executionClient);

        await Assert.ThrowsAsync<BookIdNotFoundException>(
            async () => await service.ArchiveAsync(userId: 10, bookId: 404));

        Assert.Empty(executionClient.CancelCalls);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static ILibraryService CreateService(
        FakeBookRepository repository,
        FakeDownloadJobRepository? jobRepository = null,
        FakeUnitOfWork? unitOfWork = null,
        FakeDownloadExecutionClient? executionClient = null)
    {
        return new LibraryService(
            repository,
            jobRepository ?? new FakeDownloadJobRepository(),
            unitOfWork ?? new FakeUnitOfWork(),
            executionClient ?? new FakeDownloadExecutionClient());
    }

    private static Book CreateBook(long id, CatalogState state, string title)
    {
        var book = new Book("fantlab", id.ToString(), title);
        SetProperty(book, "Id", id);
        SetProperty(book, "CatalogState", state);
        if (state == CatalogState.Library)
        {
            book.UpsertMediaAsset(MediaType.Text, "https://tracker.example/item", "jackett");
        }

        return book;
    }

    private static DownloadJob CreateJob(
        long id,
        long userId,
        long bookId,
        MediaType mediaType,
        DownloadJobStatus status,
        string? externalJobId)
    {
        var job = new DownloadJob(userId, bookId, mediaType, "jackett", "magnet:?xt=urn:btih:test");
        SetProperty(job, "Id", id);

        var updatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
        if (!string.IsNullOrWhiteSpace(externalJobId))
        {
            job.SetExternalJobId(externalJobId, updatedAtUtc);
        }

        switch (status)
        {
            case DownloadJobStatus.Queued:
                return job;
            case DownloadJobStatus.Downloading:
                job.TransitionTo(DownloadJobStatus.Downloading, updatedAtUtc.AddMinutes(1));
                return job;
            case DownloadJobStatus.Completed:
                job.TransitionTo(DownloadJobStatus.Downloading, updatedAtUtc.AddMinutes(1));
                job.TransitionTo(DownloadJobStatus.Completed, updatedAtUtc.AddMinutes(2));
                return job;
            case DownloadJobStatus.Failed:
                job.TransitionTo(DownloadJobStatus.Failed, updatedAtUtc.AddMinutes(1), "provider_error");
                return job;
            case DownloadJobStatus.Canceled:
                job.TransitionTo(DownloadJobStatus.Canceled, updatedAtUtc.AddMinutes(1));
                return job;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported job status.");
        }
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
        public List<Book> Books { get; set; } = [];

        public Task<Book?> GetByIdAsync(long bookId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Books.SingleOrDefault(x => x.Id == bookId));
        }

        public Task<Book?> GetByProviderKeyAsync(
            string providerCode,
            string providerBookKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Books.SingleOrDefault(x =>
                x.ProviderCode == providerCode && x.ProviderBookKey == providerBookKey));
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
            IEnumerable<Book> result = Books;
            if (!includeArchived)
            {
                result = result.Where(x => x.CatalogState == CatalogState.Library);
            }

            if (catalogState.HasValue)
            {
                result = result.Where(x => x.CatalogState == catalogState.Value);
            }

            if (!string.IsNullOrWhiteSpace(providerCode))
            {
                result = result.Where(x => x.ProviderCode == providerCode);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var normalizedQuery = query.Trim().ToLowerInvariant();
                result = result.Where(x =>
                    x.Title.ToLowerInvariant().Contains(normalizedQuery, StringComparison.Ordinal) ||
                    (!string.IsNullOrWhiteSpace(x.OriginalTitle) &&
                     x.OriginalTitle.ToLowerInvariant().Contains(normalizedQuery, StringComparison.Ordinal)));
            }

            var safePage = page < 1 ? 1 : page;
            var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            result = result
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize);

            return Task.FromResult<IReadOnlyList<Book>>(result.ToArray());
        }

        public Task<int> CountLibraryAsync(
            bool includeArchived,
            string? query,
            string? providerCode,
            CatalogState? catalogState,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<Book> result = Books;
            if (!includeArchived)
            {
                result = result.Where(x => x.CatalogState == CatalogState.Library);
            }

            if (catalogState.HasValue)
            {
                result = result.Where(x => x.CatalogState == catalogState.Value);
            }

            if (!string.IsNullOrWhiteSpace(providerCode))
            {
                result = result.Where(x => x.ProviderCode == providerCode);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var normalizedQuery = query.Trim().ToLowerInvariant();
                result = result.Where(x =>
                    x.Title.ToLowerInvariant().Contains(normalizedQuery, StringComparison.Ordinal) ||
                    (!string.IsNullOrWhiteSpace(x.OriginalTitle) &&
                     x.OriginalTitle.ToLowerInvariant().Contains(normalizedQuery, StringComparison.Ordinal)));
            }

            return Task.FromResult(result.Count());
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

    private sealed class FakeDownloadJobRepository : IDownloadJobRepository
    {
        public List<DownloadJob> Jobs { get; } = [];

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

        public Task<IReadOnlyList<DownloadJob>> ListByUserAndBookAsync(
            long userId,
            long bookId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DownloadJob> items = Jobs
                .Where(x => x.UserId == userId && x.BookId == bookId)
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

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeDownloadExecutionClient : IDownloadExecutionClient
    {
        public List<(string ExternalJobId, bool DeleteFiles)> CancelCalls { get; } = [];

        public TimeSpan NotFoundGracePeriod => TimeSpan.FromSeconds(60);

        public Task<DownloadEnqueueResult> EnqueueAsync(
            string downloadUri,
            string candidateId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DownloadEnqueueResult("hash"));
        }

        public Task<DownloadStatusResult> GetStatusAsync(
            string externalJobId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DownloadStatusResult(ExternalDownloadState.Downloading, null, null));
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
