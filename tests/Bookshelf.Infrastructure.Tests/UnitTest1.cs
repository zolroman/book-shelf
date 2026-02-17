using System.Net;
using System.Net.Http;
using System.Text;
using Bookshelf.Domain.Abstractions;
using Bookshelf.Infrastructure.Options;
using Bookshelf.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
}
