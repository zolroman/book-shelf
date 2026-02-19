using System.Net;
using System.Text;
using Bookshelf.Application.Exceptions;
using Bookshelf.Infrastructure.Integrations.Jackett;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bookshelf.Infrastructure.Tests;

public class JackettCandidateProviderTests
{
    [Fact]
    public async Task SearchAsync_ParsesCandidates_AndPreservesDetailsAsSourceUrl()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "Results": [
                        {
                          "Title": "Dune audiobook m4b",
                          "Guid": "https://tracker.example/guid/1",
                          "Link": "https://tracker.example/download/1",
                          "Details": "https://tracker.example/details/1",
                          "MagnetUri": "magnet:?xt=urn:btih:ABC123",
                          "Seeders": 52,
                          "Size": 734003200,
                          "PublishDate": "2024-01-01T00:00:00Z"
                        },
                        {
                          "Title": "Dune audiobook m4b duplicate",
                          "Guid": "https://tracker.example/guid/1",
                          "Link": "https://tracker.example/download/1b",
                          "Details": "https://tracker.example/details/1b",
                          "Seeders": 40
                        },
                        {
                          "Title": "Dune epub",
                          "Guid": "https://tracker.example/guid/2",
                          "Link": "https://tracker.example/download/2",
                          "Seeders": 21
                        },
                        {
                          "Link": "https://tracker.example/download/3",
                          "Guid": "https://tracker.example/guid/3",
                          "Details": "https://tracker.example/details/3"
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        var provider = CreateProvider(handler, options =>
        {
            options.BaseUrl = "http://jackett.test";
            options.ApiKey = "test-key";
            options.Indexer = "all";
            options.MaxItems = 50;
        });

        var response = await provider.SearchAsync(" Dune   Herbert ", 50);

        Assert.Equal(
            "/api/v2.0/indexers/all/results?apikey=test-key&Query=Dune%20Herbert",
            Assert.Single(handler.Requests));
        Assert.Equal(2, response.Count);

        var first = response[0];
        Assert.Equal("Dune audiobook m4b", first.Title);
        Assert.Equal("magnet:?xt=urn:btih:ABC123", first.DownloadUri);
        Assert.Equal("https://tracker.example/details/1", first.SourceUrl);
        Assert.Equal("https://tracker.example/guid/1", first.UniqueIdentifier);
        Assert.Equal(52, first.Seeders);
        Assert.Equal(734003200, first.SizeBytes);

        var second = response[1];
        Assert.Equal("Dune epub", second.Title);
        Assert.Equal("https://tracker.example/download/2", second.DownloadUri);
        Assert.Equal("https://tracker.example/guid/2", second.SourceUrl);
        Assert.Equal("https://tracker.example/guid/2", second.UniqueIdentifier);
    }

    [Fact]
    public async Task SearchAsync_RetriesTransientHttpFailure()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "Results": [
                        {
                          "Title": "Dune audiobook",
                          "Guid": "https://tracker.example/guid/1",
                          "Link": "https://tracker.example/download/1",
                          "Details": "https://tracker.example/details/1"
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        var provider = CreateProvider(handler, options =>
        {
            options.BaseUrl = "http://jackett.test";
            options.ApiKey = "test-key";
            options.MaxRetries = 1;
            options.RetryDelayMs = 1;
        });

        var response = await provider.SearchAsync("Dune", 50);

        Assert.Single(response);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SearchAsync_WithoutApiKey_ThrowsProviderUnavailable()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"Results":[]}""", Encoding.UTF8, "application/json")
            });

        var provider = CreateProvider(handler, options =>
        {
            options.BaseUrl = "http://jackett.test";
            options.ApiKey = string.Empty;
        });

        await Assert.ThrowsAsync<DownloadCandidateProviderUnavailableException>(
            async () => await provider.SearchAsync("Dune", 10));
        Assert.Empty(handler.Requests);
    }

    private static JackettCandidateProvider CreateProvider(
        SequenceHttpHandler handler,
        Action<JackettOptions>? configure = null)
    {
        var options = new JackettOptions
        {
            BaseUrl = "http://jackett.test",
            ApiKey = "test-key",
            Indexer = "all",
            TimeoutSeconds = 15,
            MaxRetries = 2,
            RetryDelayMs = 1,
            MaxItems = 50,
        };

        configure?.Invoke(options);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };

        return new JackettCandidateProvider(
            httpClient,
            Options.Create(options),
            NullLogger<JackettCandidateProvider>.Instance);
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
                    Content = new StringContent("""{"Results":[]}""", Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
