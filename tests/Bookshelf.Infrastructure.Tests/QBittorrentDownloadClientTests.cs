using System.Net;
using System.Text;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Exceptions;
using Bookshelf.Infrastructure.Integrations.QBittorrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bookshelf.Infrastructure.Tests;

public class QBittorrentDownloadClientTests
{
    [Fact]
    public async Task EnqueueAsync_PostsTorrentWithTag_AndUsesCandidateIdAsExternalJobId()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain")
            });

        var client = CreateClient(handler, options =>
        {
            options.BaseUrl = "http://qb.test";
        });

        var result = await client.EnqueueAsync(
            "magnet:?xt=urn:btih:ABCDEF1234567890&dn=Dune",
            "jackett:candidate-1");

        Assert.Equal("jackett:candidate-1", result.ExternalJobId);
        Assert.Equal("/api/v2/torrents/add", Assert.Single(handler.RequestPaths));
        Assert.Contains("urls=magnet%3A%3Fxt%3Durn%3Abtih%3AABCDEF1234567890", Assert.Single(handler.RequestBodies));
        Assert.Contains("tags=jackett%3Acandidate-1", Assert.Single(handler.RequestBodies));
    }

    [Fact]
    public async Task EnqueueAsync_RetriesOnTransientStatus()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK)
        );

        var client = CreateClient(handler, options =>
        {
            options.BaseUrl = "http://qb.test";
            options.MaxRetries = 1;
            options.RetryDelayMs = 1;
        });

        await client.EnqueueAsync("magnet:?xt=urn:btih:abc123", "jackett:candidate-retry");

        Assert.Equal(2, handler.RequestPaths.Count);
    }

    [Fact]
    public async Task EnqueueAsync_NonMagnetUri_UsesCandidateIdAsExternalJobId()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain")
            });

        var client = CreateClient(handler, options =>
        {
            options.BaseUrl = "http://qb.test";
            options.NotFoundGraceSeconds = 1;
        });

        var result = await client.EnqueueAsync(
            "http://jackett.test/dl/rutracker/abc",
            "jackett:candidate-2");

        Assert.Equal("jackett:candidate-2", result.ExternalJobId);
        Assert.Equal("/api/v2/torrents/add", Assert.Single(handler.RequestPaths));
        Assert.Contains("urls=http%3A%2F%2Fjackett.test%2Fdl%2Frutracker%2Fabc", Assert.Single(handler.RequestBodies));
        Assert.Contains("tags=jackett%3Acandidate-2", Assert.Single(handler.RequestBodies));
    }

    [Fact]
    public async Task EnqueueAsync_NonTransientStatusThrowsFailed()
    {
        var handler = new SequenceHttpHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<DownloadExecutionFailedException>(
            async () => await client.EnqueueAsync("magnet:?xt=urn:btih:abc123", "jackett:candidate-3"));
    }

    [Fact]
    public async Task GetStatusAsync_MapsCompletedStateAndMetadata()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    [
                      {
                        "hash": "abc123",
                        "state": "uploading",
                        "content_path": "D:\\media\\dune.m4b",
                        "total_size": 734003200
                      }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        var client = CreateClient(handler);

        var result = await client.GetStatusAsync("jackett:candidate-status");

        Assert.Equal(ExternalDownloadState.Completed, result.State);
        Assert.Equal("D:\\media\\dune.m4b", result.StoragePath);
        Assert.Equal(734003200, result.SizeBytes);
        Assert.Equal("/api/v2/torrents/info", Assert.Single(handler.RequestPaths));
        Assert.Contains("tag=jackett%3Acandidate-status", Assert.Single(handler.RequestQueries));
    }

    [Fact]
    public async Task GetStatusAsync_EmptyListReturnsNotFound()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });

        var client = CreateClient(handler);

        var result = await client.GetStatusAsync("jackett:candidate-empty");

        Assert.Equal(ExternalDownloadState.NotFound, result.State);
        Assert.Contains("tag=jackett%3Acandidate-empty", Assert.Single(handler.RequestQueries));
    }

    [Fact]
    public async Task CancelAsync_PostsDeleteRequest()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    [
                      { "hash": "hash789", "added_on": 1890000000 }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.CancelAsync("jackett:candidate-cancel", deleteFiles: false);

        Assert.Equal(2, handler.RequestPaths.Count);
        Assert.Equal("/api/v2/torrents/info", handler.RequestPaths[0]);
        Assert.Contains("tag=jackett%3Acandidate-cancel", handler.RequestQueries[0]);
        Assert.Equal("/api/v2/torrents/delete", handler.RequestPaths[1]);
        Assert.Contains("hashes=hash789", handler.RequestBodies[1]);
        Assert.Contains("deleteFiles=false", handler.RequestBodies[1]);
    }

    private static QBittorrentDownloadClient CreateClient(
        SequenceHttpHandler handler,
        Action<QBittorrentOptions>? configure = null)
    {
        var options = new QBittorrentOptions
        {
            BaseUrl = "http://qb.test",
            AuthMode = "none",
            TimeoutSeconds = 15,
            MaxRetries = 2,
            RetryDelayMs = 1,
            NotFoundGraceSeconds = 60,
        };

        configure?.Invoke(options);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };

        return new QBittorrentDownloadClient(
            httpClient,
            Options.Create(options),
            NullLogger<QBittorrentDownloadClient>.Instance);
    }

    private sealed class SequenceHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHttpHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<string> RequestPaths { get; } = new();
        
        public List<string> RequestQueries { get; } = new();

        public List<string> RequestBodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestPaths.Add(request.RequestUri!.AbsolutePath);
            RequestQueries.Add(request.RequestUri!.Query);
            var content = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(content);

            if (_responses.Count == 0)
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return _responses.Dequeue();
        }
    }
}
