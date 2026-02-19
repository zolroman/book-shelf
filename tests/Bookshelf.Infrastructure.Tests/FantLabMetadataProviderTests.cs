using System.Net;
using System.Text;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Exceptions;
using Bookshelf.Infrastructure.Integrations.FantLab;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bookshelf.Infrastructure.Tests;

public class FantLabMetadataProviderTests
{
    [Fact]
    public async Task SearchAsync_NormalizesQuery_AndMapsResponse()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "total": 1,
                      "items": [
                        {
                          "id": "123",
                          "title": "Dune Messiah",
                          "authors": [ "Frank Herbert" ],
                          "series": { "id": "777", "title": "Dune", "order": 2 }
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        var provider = CreateProvider(handler, options =>
        {
            options.BaseUrl = "http://fantlab.test";
            options.SearchPath = "/api/search";
        });

        var result = await provider.SearchAsync(
            new MetadataSearchRequest("  Dune   Messiah ", " Frank   Herbert ", 1));

        Assert.Equal(1, result.Total);
        var item = Assert.Single(result.Items);
        Assert.Equal("123", item.ProviderBookKey);
        Assert.Equal("Dune Messiah", item.Title);
        Assert.Equal("Frank Herbert", Assert.Single(item.Authors));
        Assert.NotNull(item.Series);
        Assert.Equal("777", item.Series!.ProviderSeriesKey);
        Assert.Equal("/api/search?q=Dune%20Messiah&author=Frank%20Herbert&page=1", handler.Requests.Single());
    }

    [Fact]
    public async Task SearchAsync_RejectsFantLabArrayPayloadShape()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    [
                      {
                        "all_autor_rusname": "Вадим Панов",
                        "autor1_rusname": "Вадим Панов",
                        "fullname": " Московский клуб   ",
                        "rusname": "Московский клуб",
                        "work_id": 4333,
                        "year": 2005
                      },
                      {
                        "all_autor_rusname": "",
                        "fullname": " Московский фан-клуб \"Звездных войн\" объявляет конкурс фан-арта по тематике ЗВ   ",
                        "rusname": "Московский фан-клуб \"Звездных войн\" объявляет конкурс фан-арта по тематике ЗВ",
                        "work_id": 1000609,
                        "year": 0
                      }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        var provider = CreateProvider(handler, options =>
        {
            options.BaseUrl = "http://fantlab.test";
            options.SearchPath = "/api/search";
        });

        await Assert.ThrowsAsync<MetadataProviderUnavailableException>(
            async () => await provider.SearchAsync(new MetadataSearchRequest("Московский клуб", null, 1)));
    }

    [Fact]
    public async Task SearchAsync_ParsesFantLabMatchesEnvelopePayloadShape()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "matches": [
                        {
                          "work_id": 9786,
                          "rusname": "Quicksilver RU",
                          "name": "Quicksilver",
                          "fullname": " Quicksilver  ",
                          "all_autor_name": "Neal Stephenson",
                          "all_autor_rusname": "Neal Stephenson"
                        }
                      ],
                      "total": 54,
                      "total_found": 54,
                      "type": "works"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        var provider = CreateProvider(handler, options =>
        {
            options.BaseUrl = "http://fantlab.test";
            options.SearchPath = "/api/search";
        });

        var result = await provider.SearchAsync(
            new MetadataSearchRequest("Quicksilver", null, 1));

        Assert.Equal(54, result.Total);
        var item = Assert.Single(result.Items);
        Assert.Equal("9786", item.ProviderBookKey);
        Assert.Equal("Quicksilver RU", item.Title);
        Assert.Contains("Neal Stephenson", item.Authors);
    }

    [Fact]
    public async Task SearchAsync_RetriesTransientFailure_ThenSucceeds()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "items": [
                        { "id": "987", "title": "Hyperion" }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        var provider = CreateProvider(handler, options =>
        {
            options.BaseUrl = "http://fantlab.test";
            options.SearchPath = "/api/search";
            options.MaxRetries = 1;
            options.RetryDelayMs = 1;
        });

        var result = await provider.SearchAsync(new MetadataSearchRequest("Hyperion", null, 1));

        Assert.Single(result.Items);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetDetailsAsync_ParsesAuthorsAndSeries()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "item": {
                        "id": "555",
                        "title": "Dune",
                        "originalTitle": "Dune",
                        "description": "Sci-fi classic",
                        "publishYear": 1965,
                        "coverUrl": "https://images.example/dune.jpg",
                        "authors": [ { "name": "Frank Herbert" } ],
                        "series": { "id": "10", "title": "Dune", "order": 1 }
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        var provider = CreateProvider(handler, options =>
        {
            options.BaseUrl = "http://fantlab.test";
            options.BookDetailsPath = "/api/work/{bookKey}";
        });

        var details = await provider.GetDetailsAsync("555");

        Assert.NotNull(details);
        Assert.Equal("555", details!.ProviderBookKey);
        Assert.Equal("Dune", details.Title);
        Assert.Equal("Frank Herbert", Assert.Single(details.Authors));
        Assert.NotNull(details.Series);
        Assert.Equal(1, details.Series!.Order);
        Assert.Equal("/api/work/555", handler.Requests.Single());
    }

    [Fact]
    public async Task SearchAsync_OpensCircuitBreaker_AfterThreshold()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "items": [
                        { "id": "1", "title": "Fallback" }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        var provider = CreateProvider(handler, options =>
        {
            options.BaseUrl = "http://fantlab.test";
            options.SearchPath = "/api/search";
            options.MaxRetries = 0;
            options.CircuitBreakerFailureThreshold = 1;
            options.CircuitBreakerOpenSeconds = 60;
        });

        await Assert.ThrowsAsync<MetadataProviderUnavailableException>(
            async () => await provider.SearchAsync(new MetadataSearchRequest("Book", null, 1)));

        await Assert.ThrowsAsync<MetadataProviderUnavailableException>(
            async () => await provider.SearchAsync(new MetadataSearchRequest("Book", null, 1)));

        Assert.Single(handler.Requests);
    }

    private static FantLabMetadataProvider CreateProvider(
        SequenceHttpHandler handler,
        Action<FantLabOptions>? configure = null)
    {
        var options = new FantLabOptions
        {
            Enabled = true,
            BaseUrl = "http://fantlab.test",
            SearchPath = "/search",
            BookDetailsPath = "/work/{bookKey}",
            TimeoutSeconds = 10,
            MaxRetries = 2,
            RetryDelayMs = 1,
            CacheEnabled = true,
            SearchCacheMinutes = 10,
            DetailsCacheHours = 24,
            CircuitBreakerFailureThreshold = 3,
            CircuitBreakerOpenSeconds = 60,
        };

        configure?.Invoke(options);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };

        return new FantLabMetadataProvider(
            httpClient,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(options),
            NullLogger<FantLabMetadataProvider>.Instance);
    }

    private sealed class SequenceHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHttpHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.PathAndQuery);
            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"items":[{"id":"default","title":"Default"}]}""", Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
