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
                    <rss>
                      <channel>
                        <item>
                          <title>Dune audiobook m4b</title>
                          <link>https://tracker.example/download/1</link>
                          <guid>https://tracker.example/guid/1</guid>
                          <pubDate>Mon, 01 Jan 2024 00:00:00 GMT</pubDate>
                          <attr name="magneturl" value="magnet:?xt=urn:btih:ABC123" />
                          <attr name="details" value="https://tracker.example/details/1" />
                          <attr name="seeders" value="52" />
                          <attr name="size" value="734003200" />
                        </item>
                        <item>
                          <title>Dune epub</title>
                          <link>https://tracker.example/download/2</link>
                          <guid>https://tracker.example/guid/2</guid>
                          <attr name="seeders" value="21" />
                        </item>
                        <item>
                          <link>https://tracker.example/download/3</link>
                          <guid>https://tracker.example/guid/3</guid>
                          <attr name="details" value="https://tracker.example/details/3" />
                        </item>
                      </channel>
                    </rss>
                    """,
                    Encoding.UTF8,
                    "application/xml")
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
            "/api/v2.0/indexers/all/results/torznab/api?apikey=test-key&t=search&q=Dune%20Herbert",
            Assert.Single(handler.Requests));
        Assert.Equal(2, response.Count);

        var first = response[0];
        Assert.Equal("Dune audiobook m4b", first.Title);
        Assert.Equal("magnet:?xt=urn:btih:ABC123", first.DownloadUri);
        Assert.Equal("https://tracker.example/details/1", first.SourceUrl);
        Assert.Equal(52, first.Seeders);
        Assert.Equal(734003200, first.SizeBytes);

        var second = response[1];
        Assert.Equal("Dune epub", second.Title);
        Assert.Equal("https://tracker.example/download/2", second.DownloadUri);
        Assert.Equal("https://tracker.example/guid/2", second.SourceUrl);
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
                    <rss>
                      <channel>
                        <item>
                          <title>Dune audiobook</title>
                          <link>https://tracker.example/download/1</link>
                          <guid>https://tracker.example/guid/1</guid>
                          <attr name="details" value="https://tracker.example/details/1" />
                        </item>
                      </channel>
                    </rss>
                    """,
                    Encoding.UTF8,
                    "application/xml")
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
                Content = new StringContent("<rss><channel /></rss>", Encoding.UTF8, "application/xml")
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
            Enabled = true,
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
                    Content = new StringContent("<rss><channel /></rss>", Encoding.UTF8, "application/xml"),
                });
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
