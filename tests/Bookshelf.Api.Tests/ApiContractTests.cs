using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Api.Api.Middleware;
using Bookshelf.Shared.Contracts.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bookshelf.Api.Tests;

public class ApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Search_WithoutQuery_ReturnsQueryRequired()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search/books");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("QUERY_REQUIRED", payload!.Code);
    }

    [Fact]
    public async Task ErrorResponse_ContainsPropagatedCorrelationId()
    {
        const string correlationId = "test-correlation-id";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var response = await client.GetAsync("/api/v1/search/books");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var headerValues));
        Assert.Contains(correlationId, headerValues!);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal(correlationId, payload!.CorrelationId);
    }

    [Fact]
    public async Task CreateShelf_DuplicateName_ReturnsConflict()
    {
        var shelfService = new InMemoryShelfService();
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IShelfService>();
                services.AddSingleton<IShelfService>(shelfService);
            });
        });

        using var client = factory.CreateClient();
        var userId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var request = new CreateShelfRequest(userId, "Sci-Fi");

        var first = await client.PostAsJsonAsync("/api/v1/shelves", request);
        var second = await client.PostAsJsonAsync("/api/v1/shelves", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var payload = await second.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("SHELF_NAME_CONFLICT", payload!.Code);
    }

    [Fact]
    public async Task AddBookToShelf_DuplicateBook_ReturnsConflict()
    {
        var shelfService = new InMemoryShelfService();
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IShelfService>();
                services.AddSingleton<IShelfService>(shelfService);
            });
        });

        using var client = factory.CreateClient();
        var userId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/shelves",
            new CreateShelfRequest(userId, "Favorites"));

        var created = await createResponse.Content.ReadFromJsonAsync<CreateShelfResponse>();
        Assert.NotNull(created);
        var shelfId = created!.Shelf.Id;

        var request = new AddBookToShelfRequest(userId, 42);
        var first = await client.PostAsJsonAsync($"/api/v1/shelves/{shelfId}/books", request);
        var second = await client.PostAsJsonAsync($"/api/v1/shelves/{shelfId}/books", request);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var payload = await second.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("SHELF_BOOK_EXISTS", payload!.Code);
    }

    [Fact]
    public async Task Candidates_InvalidMediaType_ReturnsInvalidArgument()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search/books/fantlab/123/candidates?mediaType=video");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("INVALID_ARGUMENT", payload!.Code);
    }

    [Fact]
    public async Task Details_UnsupportedProvider_ReturnsInvalidArgument()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search/books/other/123");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("INVALID_ARGUMENT", payload!.Code);
    }

    [Fact]
    public async Task Candidates_ReturnsServicePayload()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICandidateDiscoveryService>();
                services.AddScoped<ICandidateDiscoveryService>(_ => new StubCandidateDiscoveryService());
            });
        });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search/books/fantlab/123/candidates?mediaType=audio&page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<DownloadCandidatesResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Total);
        var item = Assert.Single(payload.Items);
        Assert.Equal("jackett:abc123", item.CandidateId);
        Assert.Equal("audio", item.MediaType);
    }

    [Fact]
    public async Task Candidates_WhenJackettUnavailable_ReturnsMappedError()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICandidateDiscoveryService>();
                services.AddScoped<ICandidateDiscoveryService>(_ => new ThrowingCandidateDiscoveryService(
                    new DownloadCandidateProviderUnavailableException("jackett", "down")));
            });
        });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search/books/fantlab/123/candidates?mediaType=audio");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("JACKETT_UNAVAILABLE", payload!.Code);
    }

    [Fact]
    public async Task Candidates_WhenFantLabUnavailable_ReturnsMappedError()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICandidateDiscoveryService>();
                services.AddScoped<ICandidateDiscoveryService>(_ => new ThrowingCandidateDiscoveryService(
                    new MetadataProviderUnavailableException("fantlab", "down")));
            });
        });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search/books/fantlab/123/candidates?mediaType=audio");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("FANTLAB_UNAVAILABLE", payload!.Code);
    }

    [Fact]
    public async Task AddAndDownload_WhenCandidateNotFound_ReturnsMappedError()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAddAndDownloadService>();
                services.AddScoped<IAddAndDownloadService>(_ => new ThrowingAddAndDownloadService(
                    new DownloadCandidateNotFoundException("missing")));
            });
        });

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/library/add-and-download",
            new AddAndDownloadRequest(1, "fantlab", "123", "audio", "jackett:missing"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("CANDIDATE_NOT_FOUND", payload!.Code);
    }

    [Fact]
    public async Task AddAndDownload_WhenQBittorrentUnavailable_ReturnsMappedError()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAddAndDownloadService>();
                services.AddScoped<IAddAndDownloadService>(_ => new ThrowingAddAndDownloadService(
                    new DownloadExecutionUnavailableException("qbittorrent", "down")));
            });
        });

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/library/add-and-download",
            new AddAndDownloadRequest(1, "fantlab", "123", "audio", "jackett:abc"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("QBITTORRENT_UNAVAILABLE", payload!.Code);
    }

    [Fact]
    public async Task CancelDownloadJob_WhenCancelFails_ReturnsMappedError()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDownloadJobService>();
                services.AddScoped<IDownloadJobService>(_ => new ThrowingDownloadJobService(
                    new DownloadExecutionFailedException("qbittorrent", "cancel failed")));
            });
        });

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/download-jobs/5/cancel",
            new CancelDownloadJobRequest(1));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("DOWNLOAD_CANCEL_FAILED", payload!.Code);
    }

    [Fact]
    public async Task ListDownloadJobs_InvalidStatus_ReturnsInvalidArgument()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDownloadJobService>();
                services.AddScoped<IDownloadJobService>(_ => new ThrowingDownloadJobService(
                    new ArgumentException("invalid status")));
            });
        });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/download-jobs?userId=1&status=wrong");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("INVALID_ARGUMENT", payload!.Code);
    }

    [Fact]
    public async Task Library_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/library");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Library_WithToken_ReturnsPayload()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILibraryService>();
                services.AddScoped<ILibraryService>(_ => new StubLibraryService());
            });
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "uid:12");

        var response = await client.GetAsync(
            "/api/v1/library?includeArchived=true&page=2&pageSize=5&query=dune&providerCode=fantlab&catalogState=library");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LibraryResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Page);
        Assert.Equal(5, payload.PageSize);
        Assert.Equal(1, payload.Total);
        Assert.True(payload.IncludeArchived);
        Assert.Single(payload.Items);
    }

    [Fact]
    public async Task Progress_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/progress");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Progress_WithToken_ReturnsSnapshot()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProgressHistoryService>();
                services.AddScoped<IProgressHistoryService>(_ => new StubProgressHistoryService());
            });
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "uid:21");

        var response = await client.PutAsJsonAsync(
            "/api/v1/progress",
            new UpsertProgressRequest(
                BookId: 42,
                MediaType: "text",
                PositionRef: "page:10",
                ProgressPercent: 12.5m,
                UpdatedAtUtc: new DateTimeOffset(2026, 2, 18, 12, 0, 0, TimeSpan.Zero)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ProgressSnapshotDto>();
        Assert.NotNull(payload);
        Assert.Equal(21, payload!.UserId);
        Assert.Equal(42, payload.BookId);
    }

    [Fact]
    public async Task History_WithToken_ReturnsAppendResponse()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProgressHistoryService>();
                services.AddScoped<IProgressHistoryService>(_ => new StubProgressHistoryService());
            });
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "uid:21");

        var response = await client.PostAsJsonAsync(
            "/api/v1/history/events",
            new AppendHistoryEventsRequest(
                [
                    new HistoryEventWriteDto(
                        BookId: 42,
                        MediaType: "text",
                        EventType: "progress",
                        PositionRef: "page:10",
                        EventAtUtc: new DateTimeOffset(2026, 2, 18, 12, 5, 0, TimeSpan.Zero)),
                ]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AppendHistoryEventsResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Added);
    }

    private sealed class StubCandidateDiscoveryService : ICandidateDiscoveryService
    {
        public Task<DownloadCandidatesResponse> FindAsync(
            string providerCode,
            string providerBookKey,
            string mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new DownloadCandidatesResponse(
                    providerCode,
                    providerBookKey,
                    mediaType,
                    page,
                    pageSize,
                    1,
                    new[]
                    {
                        new DownloadCandidateDto(
                            "jackett:abc123",
                            "audio",
                            "Dune Audiobook",
                            "magnet:?xt=urn:btih:abc123",
                            "https://tracker.example/item/123",
                            52,
                            734003200),
                    }));
        }

        public Task<DownloadCandidateDto?> ResolveAsync(
            string providerCode,
            string providerBookKey,
            string mediaType,
            string candidateId,
            CancellationToken cancellationToken = default)
        {
            if (candidateId.Equals("jackett:abc123", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<DownloadCandidateDto?>(
                    new DownloadCandidateDto(
                        "jackett:abc123",
                        "audio",
                        "Dune Audiobook",
                        "magnet:?xt=urn:btih:abc123",
                        "https://tracker.example/item/123",
                        52,
                        734003200));
            }

            return Task.FromResult<DownloadCandidateDto?>(null);
        }
    }

    private sealed class ThrowingCandidateDiscoveryService : ICandidateDiscoveryService
    {
        private readonly Exception _exception;

        public ThrowingCandidateDiscoveryService(Exception exception)
        {
            _exception = exception;
        }

        public Task<DownloadCandidatesResponse> FindAsync(
            string providerCode,
            string providerBookKey,
            string mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }

        public Task<DownloadCandidateDto?> ResolveAsync(
            string providerCode,
            string providerBookKey,
            string mediaType,
            string candidateId,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed class ThrowingAddAndDownloadService : IAddAndDownloadService
    {
        private readonly Exception _exception;

        public ThrowingAddAndDownloadService(Exception exception)
        {
            _exception = exception;
        }

        public Task<AddAndDownloadResponse> ExecuteAsync(
            AddAndDownloadRequest request,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed class ThrowingDownloadJobService : IDownloadJobService
    {
        private readonly Exception _exception;

        public ThrowingDownloadJobService(Exception exception)
        {
            _exception = exception;
        }

        public Task<DownloadJobsResponse> ListAsync(
            long userId,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }

        public Task<DownloadJobDto?> GetAsync(
            long jobId,
            long userId,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }

        public Task<DownloadJobDto> CancelAsync(
            long jobId,
            long userId,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }

        public Task SyncActiveAsync(CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed class StubLibraryService : ILibraryService
    {
        public Task<LibraryResponse> ListAsync(
            long userId,
            bool includeArchived,
            string? query,
            string? providerCode,
            string? catalogState,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new LibraryResponse(
                    Page: page,
                    PageSize: pageSize,
                    Total: 1,
                    IncludeArchived: includeArchived,
                    Items:
                    [
                        new LibraryBookDto(
                            Id: 42,
                            ProviderCode: "fantlab",
                            ProviderBookKey: "123",
                            Title: "Dune",
                            OriginalTitle: "Dune",
                            Description: "Sci-fi classic",
                            PublishYear: 1965,
                            LanguageCode: "en",
                            CoverUrl: "https://images.example/dune.jpg",
                            HasTextMedia: true,
                            HasAudioMedia: false,
                            CatalogState: "library",
                            CreatedAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                            UpdatedAtUtc: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)),
                    ]));
        }
    }

    private sealed class StubProgressHistoryService : IProgressHistoryService
    {
        public Task<ProgressSnapshotDto> UpsertProgressAsync(
            long userId,
            UpsertProgressRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new ProgressSnapshotDto(
                    UserId: userId,
                    BookId: request.BookId,
                    MediaType: request.MediaType,
                    PositionRef: request.PositionRef,
                    ProgressPercent: request.ProgressPercent,
                    UpdatedAtUtc: request.UpdatedAtUtc ?? DateTimeOffset.UtcNow));
        }

        public Task<ProgressSnapshotsResponse> ListProgressAsync(
            long userId,
            long? bookId,
            string? mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new ProgressSnapshotsResponse(
                    Page: page,
                    PageSize: pageSize,
                    Total: 1,
                    Items:
                    [
                        new ProgressSnapshotDto(
                            UserId: userId,
                            BookId: bookId ?? 42,
                            MediaType: mediaType ?? "text",
                            PositionRef: "page:10",
                            ProgressPercent: 10m,
                            UpdatedAtUtc: DateTimeOffset.UtcNow),
                    ]));
        }

        public Task<AppendHistoryEventsResponse> AppendHistoryAsync(
            long userId,
            AppendHistoryEventsRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new AppendHistoryEventsResponse(
                    Added: request.Items.Count,
                    Deduplicated: 0));
        }

        public Task<HistoryEventsResponse> ListHistoryAsync(
            long userId,
            long? bookId,
            string? mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new HistoryEventsResponse(
                    Page: page,
                    PageSize: pageSize,
                    Total: 1,
                    Items:
                    [
                        new HistoryEventDto(
                            Id: 1,
                            UserId: userId,
                            BookId: bookId ?? 42,
                            MediaType: mediaType ?? "text",
                            EventType: "progress",
                            PositionRef: "page:10",
                            EventAtUtc: DateTimeOffset.UtcNow),
                    ]));
        }
    }

    private sealed class InMemoryShelfService : IShelfService
    {
        private readonly ConcurrentDictionary<long, ShelfDto> _shelves = new();
        private long _nextShelfId = 100;

        public Task<ShelvesResponse> ListAsync(long userId, CancellationToken cancellationToken = default)
        {
            var items = _shelves.Values
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.Name)
                .ToArray();
            return Task.FromResult(new ShelvesResponse(items));
        }

        public Task<ShelfDto?> CreateAsync(long userId, string name, CancellationToken cancellationToken = default)
        {
            var normalized = name.Trim();
            if (_shelves.Values.Any(x =>
                    x.UserId == userId &&
                    x.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult<ShelfDto?>(null);
            }

            var created = new ShelfDto(
                Id: Interlocked.Increment(ref _nextShelfId),
                UserId: userId,
                Name: normalized,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                BookIds: Array.Empty<long>());

            _shelves[created.Id] = created;
            return Task.FromResult<ShelfDto?>(created);
        }

        public Task<ShelfAddBookResult> AddBookAsync(
            long shelfId,
            long userId,
            long bookId,
            CancellationToken cancellationToken = default)
        {
            if (!_shelves.TryGetValue(shelfId, out var shelf) || shelf.UserId != userId)
            {
                return Task.FromResult(new ShelfAddBookResult(ShelfAddBookResultStatus.NotFound, null));
            }

            if (shelf.BookIds.Contains(bookId))
            {
                return Task.FromResult(new ShelfAddBookResult(ShelfAddBookResultStatus.AlreadyExists, null));
            }

            var updated = shelf with { BookIds = shelf.BookIds.Concat(new[] { bookId }).OrderBy(x => x).ToArray() };
            _shelves[shelfId] = updated;
            return Task.FromResult(new ShelfAddBookResult(ShelfAddBookResultStatus.Success, updated));
        }

        public Task<bool> RemoveBookAsync(
            long shelfId,
            long userId,
            long bookId,
            CancellationToken cancellationToken = default)
        {
            if (!_shelves.TryGetValue(shelfId, out var shelf) || shelf.UserId != userId)
            {
                return Task.FromResult(false);
            }

            var updated = shelf with { BookIds = shelf.BookIds.Where(x => x != bookId).ToArray() };
            _shelves[shelfId] = updated;
            return Task.FromResult(true);
        }
    }
}
