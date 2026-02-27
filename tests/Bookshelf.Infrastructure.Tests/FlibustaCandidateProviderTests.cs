using System.Net;
using System.Text;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Infrastructure.Integrations.Flibusta;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bookshelf.Infrastructure.Tests;

public class FlibustaCandidateProviderTests
{
    [Fact]
    public async Task SearchAsync_ParsesEpubCandidates_FiltersByAuthor_AndReadsNextPage()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    <?xml version="1.0" encoding="utf-8"?>
                    <feed xmlns="http://www.w3.org/2005/Atom" xmlns:dc="http://purl.org/dc/terms/" xmlns:os="http://a9.com/-/spec/opensearch/1.1/">
                      <entry>
                        <title>Солярис</title>
                        <author><name>Другой Автор</name></author>
                        <dc:issued>1992</dc:issued>
                        <content type="text/html">Размер: 1000 Kb</content>
                        <link href="/b/1/epub" rel="http://opds-spec.org/acquisition/open-access" type="application/epub+zip" />
                        <link href="/b/1" rel="alternate" type="text/html" />
                      </entry>
                      <os:totalResults>2</os:totalResults>
                      <os:startIndex>0</os:startIndex>
                      <os:itemsPerPage>1</os:itemsPerPage>
                    </feed>
                    """,
                    Encoding.UTF8,
                    "application/atom+xml")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    <?xml version="1.0" encoding="utf-8"?>
                    <feed xmlns="http://www.w3.org/2005/Atom" xmlns:dc="http://purl.org/dc/terms/" xmlns:os="http://a9.com/-/spec/opensearch/1.1/">
                      <entry>
                        <title>Солярис</title>
                        <author><name>Лем Станислав</name></author>
                        <dc:issued>1992</dc:issued>
                        <content type="text/html">Год издания: 1992&lt;br/&gt;Размер: 2651 Kb</content>
                        <link href="/b/659810/epub" rel="http://opds-spec.org/acquisition/open-access" type="application/epub+zip" />
                        <link href="/b/659810" rel="alternate" type="text/html" />
                      </entry>
                      <os:totalResults>2</os:totalResults>
                      <os:startIndex>1</os:startIndex>
                      <os:itemsPerPage>1</os:itemsPerPage>
                    </feed>
                    """,
                    Encoding.UTF8,
                    "application/atom+xml")
            });

        var provider = CreateProvider(handler, options => options.MaxPages = 3);

        var response = await provider.SearchAsync(
            new DownloadCandidateSearchRequest("Солярис", "Лем Станислав"),
            20);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/opds/opensearch?searchTerm=%D0%A1%D0%BE%D0%BB%D1%8F%D1%80%D0%B8%D1%81&searchType=books&pageNumber=0", handler.Requests[0]);
        Assert.Equal("/opds/opensearch?searchTerm=%D0%A1%D0%BE%D0%BB%D1%8F%D1%80%D0%B8%D1%81&searchType=books&pageNumber=1", handler.Requests[1]);

        var item = Assert.Single(response);
        Assert.Equal("Солярис [EPUB]", item.Title);
        Assert.Equal("http://flibusta.site/b/659810/epub", item.DownloadUri);
        Assert.Equal("http://flibusta.site/b/659810", item.SourceUrl);
        Assert.Equal("659810:epub", item.UniqueIdentifier);
        Assert.Equal(2651L * 1024L, item.SizeBytes);
        Assert.Equal(new DateTimeOffset(1992, 1, 1, 0, 0, 0, TimeSpan.Zero), item.PublishedAtUtc);
    }

    [Fact]
    public async Task SearchAsync_WithoutAuthorFilter_ReturnsMatchingEpubItemsFromFirstPage()
    {
        var handler = new SequenceHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    <?xml version="1.0" encoding="utf-8"?>
                    <feed xmlns="http://www.w3.org/2005/Atom" xmlns:dc="http://purl.org/dc/terms/" xmlns:os="http://a9.com/-/spec/opensearch/1.1/">
                      <entry>
                        <title>Том 2. Солярис. Возвращение со звезд</title>
                        <author><name>Лем Станислав</name></author>
                        <dc:issued>1992</dc:issued>
                        <content type="text/html">Размер: 2651 Kb</content>
                        <link href="/b/659810/fb2" rel="http://opds-spec.org/acquisition/open-access" type="application/fb2+zip" />
                        <link href="/b/659810/epub" rel="http://opds-spec.org/acquisition/open-access" type="application/epub+zip" />
                        <link href="/b/659810" rel="alternate" type="text/html" />
                      </entry>
                      <entry>
                        <title>Only FB2</title>
                        <author><name>Лем Станислав</name></author>
                        <link href="/b/42/fb2" rel="http://opds-spec.org/acquisition/open-access" type="application/fb2+zip" />
                        <link href="/b/42" rel="alternate" type="text/html" />
                      </entry>
                      <os:totalResults>2</os:totalResults>
                      <os:startIndex>0</os:startIndex>
                      <os:itemsPerPage>20</os:itemsPerPage>
                    </feed>
                    """,
                    Encoding.UTF8,
                    "application/atom+xml")
            });

        var provider = CreateProvider(handler);

        var response = await provider.SearchAsync(new DownloadCandidateSearchRequest("Solaris"), 20);

        Assert.Single(response);
        Assert.Equal("/opds/opensearch?searchTerm=Solaris&searchType=books&pageNumber=0", Assert.Single(handler.Requests));
    }

    private static FlibustaCandidateProvider CreateProvider(
        SequenceHttpHandler handler,
        Action<FlibustaOptions>? configure = null)
    {
        var options = new FlibustaOptions
        {
            BaseUrl = "http://flibusta.site",
            TimeoutSeconds = 15,
            MaxRetries = 1,
            RetryDelayMs = 1,
            MaxItems = 20,
            MaxPages = 5,
        };

        configure?.Invoke(options);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };

        return new FlibustaCandidateProvider(
            httpClient,
            Options.Create(options),
            NullLogger<FlibustaCandidateProvider>.Instance);
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
                    Content = new StringContent("""<feed xmlns="http://www.w3.org/2005/Atom"></feed>""", Encoding.UTF8, "application/atom+xml"),
                });
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
