using System.Net;
using System.Net.Http;
using System.Text;
using Bookshelf.Domain.Abstractions;
using Bookshelf.Domain.Enums;
using Bookshelf.Infrastructure.Models;
using Bookshelf.Infrastructure.Options;
using Bookshelf.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bookshelf.Infrastructure.Tests;

public class InMemoryBookshelfRepositoryTests
{
    [Fact]
    public async Task AddLibraryItem_Returns_Existing_Record_For_Duplicate_Request()
    {
        var repository = new InMemoryBookshelfRepository(new FixedClock());

        var first = await repository.AddLibraryItemAsync(1, 1, CancellationToken.None);
        var second = await repository.AddLibraryItemAsync(1, 1, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task Deleting_Local_Asset_Does_Not_Remove_History()
    {
        var repository = new InMemoryBookshelfRepository(new FixedClock());

        await repository.AddHistoryEventAsync(1, 1, Domain.Enums.BookFormatType.Text, Domain.Enums.HistoryEventType.Completed, "100%", DateTime.UtcNow, CancellationToken.None);
        await repository.AddOrUpdateLocalAssetAsync(1, 1, "local/file.epub", 42, CancellationToken.None);
        await repository.MarkLocalAssetDeletedAsync(1, 1, CancellationToken.None);

        var history = await repository.GetHistoryEventsAsync(1, 1, CancellationToken.None);

        Assert.Single(history);
    }

    [Fact]
    public async Task Deleting_Local_Asset_Does_Not_Remove_Library_Or_Progress()
    {
        var repository = new InMemoryBookshelfRepository(new FixedClock());

        await repository.AddLibraryItemAsync(1, 1, CancellationToken.None);
        await repository.UpsertProgressSnapshotAsync(1, 1, BookFormatType.Text, "c1:p10", 12.5f, CancellationToken.None);
        await repository.AddOrUpdateLocalAssetAsync(1, 1, "local/file.epub", 100, CancellationToken.None);

        await repository.MarkLocalAssetDeletedAsync(1, 1, CancellationToken.None);

        var libraryItem = await repository.GetLibraryItemAsync(1, 1, CancellationToken.None);
        var progress = await repository.GetProgressSnapshotAsync(1, 1, BookFormatType.Text, CancellationToken.None);

        Assert.NotNull(libraryItem);
        Assert.NotNull(progress);
        Assert.Equal(12.5f, progress!.ProgressPercent);
        Assert.Equal("c1:p10", progress.PositionRef);
    }

    [Fact]
    public async Task FantLabProvider_Imports_And_Normalizes_Results()
    {
        var repository = new InMemoryBookshelfRepository(new FixedClock());
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"items\":[{\"name\":\"Hyperion\",\"authors\":[{\"name\":\"Dan Simmons\"}],\"year\":1989,\"rating\":9.1,\"has_text\":true,\"has_audio\":true}]}", Encoding.UTF8, "application/json")
        });

        var provider = CreateProvider(repository, handler);

        var result = await provider.SearchAsync("Hyperion", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Hyperion", result[0].Title);

        var formats = await repository.GetFormatsForBookAsync(result[0].Id, CancellationToken.None);
        Assert.Contains(formats, x => x.FormatType == Domain.Enums.BookFormatType.Text);
        Assert.Contains(formats, x => x.FormatType == Domain.Enums.BookFormatType.Audio);
    }

    [Fact]
    public async Task FantLabProvider_Falls_Back_To_Local_Results_On_Error()
    {
        var repository = new InMemoryBookshelfRepository(new FixedClock());
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var provider = CreateProvider(repository, handler);

        var result = await provider.SearchAsync("Dune", CancellationToken.None);

        Assert.Contains(result, x => x.Title == "Dune");
        Assert.True(handler.CallCount >= 1);
    }

    [Fact]
    public async Task FantLabProvider_Caches_Repeated_Queries()
    {
        var repository = new InMemoryBookshelfRepository(new FixedClock());
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[{\"title\":\"The Left Hand of Darkness\",\"author\":\"Ursula K. Le Guin\"}]", Encoding.UTF8, "application/json")
        });

        var provider = CreateProvider(repository, handler);

        _ = await provider.SearchAsync("Left Hand", CancellationToken.None);
        _ = await provider.SearchAsync("Left Hand", CancellationToken.None);

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DownloadPipeline_Start_Is_Idempotent_For_Active_Job()
    {
        var repository = new InMemoryBookshelfRepository(new FixedClock());
        var torrentSearch = new FakeTorrentSearchClient();
        var qbClient = new FakeQbittorrentClient();
        var service = new DownloadPipelineService(
            repository,
            torrentSearch,
            qbClient,
            new FixedClock(),
            NullLogger<DownloadPipelineService>.Instance);

        var first = await service.StartAsync(1, 1, "The Martian", CancellationToken.None);
        var second = await service.StartAsync(1, 1, "The Martian", CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task DownloadPipeline_Completed_Job_Creates_Local_Asset()
    {
        var repository = new InMemoryBookshelfRepository(new FixedClock());
        var torrentSearch = new FakeTorrentSearchClient();
        var qbClient = new FakeQbittorrentClient();
        var service = new DownloadPipelineService(
            repository,
            torrentSearch,
            qbClient,
            new FixedClock(),
            NullLogger<DownloadPipelineService>.Instance);

        var job = await service.StartAsync(1, 1, "The Martian", CancellationToken.None);
        qbClient.SetStatus(job.ExternalJobId, ExternalDownloadStatus.Completed);

        var refreshed = await service.GetJobAsync(job.Id, CancellationToken.None);
        var assets = await repository.GetLocalAssetsAsync(1, CancellationToken.None);

        Assert.NotNull(refreshed);
        Assert.Equal(DownloadJobStatus.Completed, refreshed!.Status);
        Assert.Contains(assets, x => x.BookFormatId == 1 && !x.IsDeleted);
    }

    [Fact]
    public async Task DownloadPipeline_Cancel_Transitions_To_Canceled()
    {
        var repository = new InMemoryBookshelfRepository(new FixedClock());
        var torrentSearch = new FakeTorrentSearchClient();
        var qbClient = new FakeQbittorrentClient();
        var service = new DownloadPipelineService(
            repository,
            torrentSearch,
            qbClient,
            new FixedClock(),
            NullLogger<DownloadPipelineService>.Instance);

        var job = await service.StartAsync(1, 1, "The Martian", CancellationToken.None);
        var canceled = await service.CancelAsync(job.Id, CancellationToken.None);

        Assert.NotNull(canceled);
        Assert.Equal(DownloadJobStatus.Canceled, canceled!.Status);
        Assert.Contains(job.ExternalJobId, qbClient.CanceledExternalIds);
    }

    [Fact]
    public async Task JackettClient_Handles_Transient_Failure_With_Fallback()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JackettOptions
        {
            Enabled = true,
            UseMockFallback = true,
            BaseUrl = "http://localhost:9117",
            ApiKey = "test-api-key",
            Indexer = "all",
            TimeoutSeconds = 5,
            MaxItems = 5,
            MaxRetries = 1,
            RetryDelayMilliseconds = 1
        });

        var responseCount = 0;
        var handler = new FakeHandler(_ =>
        {
            responseCount++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var client = new HttpClient(handler);
        var factory = new FakeHttpClientFactory(client);
        var jackettClient = new JackettTorrentSearchClient(factory, options, NullLogger<JackettTorrentSearchClient>.Instance);

        var results = await jackettClient.SearchAsync("Dune", 5, CancellationToken.None);

        Assert.NotEmpty(results);
        Assert.True(handler.CallCount >= 1);
    }

    private static FantLabBookSearchProvider CreateProvider(InMemoryBookshelfRepository repository, HttpMessageHandler handler)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new FantLabSearchOptions
        {
            Enabled = true,
            BaseUrl = "https://api.fantlab.ru",
            SearchPath = "/search",
            QueryParameter = "query",
            MaxRetries = 1,
            RetryDelayMilliseconds = 1,
            CacheTtlMinutes = 5,
            CircuitBreakerFailureThreshold = 2,
            CircuitBreakerOpenSeconds = 60
        });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new HttpClient(handler);
        var factory = new FakeHttpClientFactory(client);

        return new FantLabBookSearchProvider(repository, cache, factory, options, NullLogger<FantLabBookSearchProvider>.Instance);
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 2, 17, 10, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        private readonly HttpClient _client = client;

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory = factory;

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_factory(request));
        }
    }

    private sealed class FakeTorrentSearchClient : ITorrentSearchClient
    {
        public Task<IReadOnlyList<TorrentCandidate>> SearchAsync(string query, int maxItems, CancellationToken cancellationToken)
        {
            var candidate = new TorrentCandidate(
                $"{query} mock",
                CreateMagnet(query),
                "test-jackett",
                Seeders: 50,
                SizeBytes: 800_000_000);

            return Task.FromResult<IReadOnlyList<TorrentCandidate>>([candidate]);
        }

        private static string CreateMagnet(string query)
        {
            var hex = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(query)));
            return $"magnet:?xt=urn:btih:{hex}&dn={Uri.EscapeDataString(query)}";
        }
    }

    private sealed class FakeQbittorrentClient : IQbittorrentDownloadClient
    {
        private readonly Dictionary<string, ExternalDownloadStatus> _states = new(StringComparer.OrdinalIgnoreCase);

        public List<string> CanceledExternalIds { get; } = [];

        public Task<string> EnqueueAsync(string downloadUri, CancellationToken cancellationToken)
        {
            var externalId = TryExtractHash(downloadUri) ?? $"test-{Guid.NewGuid():N}";
            _states[externalId] = ExternalDownloadStatus.Downloading;
            return Task.FromResult(externalId);
        }

        public Task<ExternalDownloadStatus> GetStatusAsync(string externalJobId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_states.TryGetValue(externalJobId, out var status)
                ? status
                : ExternalDownloadStatus.Unknown);
        }

        public Task CancelAsync(string externalJobId, CancellationToken cancellationToken)
        {
            CanceledExternalIds.Add(externalJobId);
            _states[externalJobId] = ExternalDownloadStatus.Canceled;
            return Task.CompletedTask;
        }

        public void SetStatus(string externalJobId, ExternalDownloadStatus status)
        {
            _states[externalJobId] = status;
        }

        private static string? TryExtractHash(string downloadUri)
        {
            if (!downloadUri.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var query = downloadUri["magnet:?".Length..];
            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!part.StartsWith("xt=urn:btih:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return part["xt=urn:btih:".Length..].ToLowerInvariant();
            }

            return null;
        }
    }
}
