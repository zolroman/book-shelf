using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Services;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Tests;

public class CandidateDiscoveryServiceTests
{
    [Fact]
    public async Task FindAsync_FiltersByMediaType_AndKeepsUnknownItems()
    {
        var service = CreateService(
            new[]
            {
                Candidate("Dune audiobook m4b", "magnet:?xt=urn:btih:audio", "https://details/audio", 30, 700L * 1024 * 1024),
                Candidate("Dune.epub", "magnet:?xt=urn:btih:text", "https://details/text", 50, 4L * 1024 * 1024),
                Candidate("Dune release", "magnet:?xt=urn:btih:unknown", "https://details/unknown", 5, 60L * 1024 * 1024),
            });

        var response = await service.FindAsync("fantlab", "123", "audio", 1, 20);

        Assert.Equal(2, response.Total);
        Assert.All(response.Items, item => Assert.Contains(item.MediaType, new[] { "audio", "unknown" }));
        Assert.DoesNotContain(response.Items, item => item.MediaType == "text");
    }

    [Fact]
    public async Task FindAsync_RanksByTitleThenSeedersThenSizeThenRecency()
    {
        var service = CreateService(
            new[]
            {
                Candidate(
                    "Dune audiobook edition",
                    "magnet:?xt=urn:btih:a",
                    "https://details/a",
                    10,
                    700L * 1024 * 1024,
                    new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                Candidate(
                    "Other audiobook release",
                    "magnet:?xt=urn:btih:b",
                    "https://details/b",
                    999,
                    700L * 1024 * 1024,
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                Candidate(
                    "Dune audiobook compact",
                    "magnet:?xt=urn:btih:c",
                    "https://details/c",
                    10,
                    10L * 1024 * 1024,
                    new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)),
            });

        var response = await service.FindAsync("fantlab", "123", "audio", 1, 20);

        Assert.Equal(3, response.Total);
        Assert.Equal("Dune audiobook edition", response.Items[0].Title);
        Assert.Equal("Dune audiobook compact", response.Items[1].Title);
        Assert.Equal("Other audiobook release", response.Items[2].Title);
    }

    [Fact]
    public async Task FindAsync_AppliesPaginationAfterRanking()
    {
        var service = CreateService(
            new[]
            {
                Candidate("Dune audiobook edition", "magnet:?xt=urn:btih:a", "https://details/a", 10, 700L * 1024 * 1024),
                Candidate("Other audiobook release", "magnet:?xt=urn:btih:b", "https://details/b", 999, 700L * 1024 * 1024),
                Candidate("Dune audiobook compact", "magnet:?xt=urn:btih:c", "https://details/c", 10, 10L * 1024 * 1024),
            });

        var response = await service.FindAsync("fantlab", "123", "audio", 2, 1);

        Assert.Equal(3, response.Total);
        Assert.Equal(2, response.Page);
        Assert.Equal(1, response.PageSize);
        var item = Assert.Single(response.Items);
        Assert.Equal("Dune audiobook compact", item.Title);
    }

    private static ICandidateDiscoveryService CreateService(IReadOnlyList<DownloadCandidateRaw> candidates)
    {
        var bookSearchService = new FakeBookSearchService(
            new SearchBookDetailsResponse(
                ProviderCode: "fantlab",
                ProviderBookKey: "123",
                Title: "Dune",
                OriginalTitle: "Dune",
                Description: null,
                PublishYear: 1965,
                CoverUrl: null,
                Authors: new[] { "Frank Herbert" },
                Series: null));

        var provider = new FakeCandidateProvider(candidates);
        return new CandidateDiscoveryService(new[] { provider }, bookSearchService);
    }

    private static DownloadCandidateRaw Candidate(
        string title,
        string downloadUri,
        string sourceUrl,
        int? seeders = null,
        long? sizeBytes = null,
        DateTimeOffset? publishedAtUtc = null)
    {
        return new DownloadCandidateRaw(
            title,
            downloadUri,
            sourceUrl,
            seeders,
            sizeBytes,
            publishedAtUtc);
    }

    private sealed class FakeCandidateProvider : IDownloadCandidateProvider
    {
        private readonly IReadOnlyList<DownloadCandidateRaw> _candidates;

        public FakeCandidateProvider(IReadOnlyList<DownloadCandidateRaw> candidates)
        {
            _candidates = candidates;
        }

        public string ProviderCode => "jackett";

        public Task<IReadOnlyList<DownloadCandidateRaw>> SearchAsync(
            string query,
            int maxItems,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DownloadCandidateRaw>>(
                _candidates.Take(maxItems).ToArray());
        }
    }

    private sealed class FakeBookSearchService : IBookSearchService
    {
        private readonly SearchBookDetailsResponse _details;

        public FakeBookSearchService(SearchBookDetailsResponse details)
        {
            _details = details;
        }

        public Task<SearchBooksResponse> SearchAsync(
            string? title,
            string? author,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new SearchBooksResponse(
                    new SearchBooksQuery(title, author),
                    page,
                    pageSize,
                    0,
                    Array.Empty<SearchBookItemDto>()));
        }

        public Task<SearchBookDetailsResponse?> GetDetailsAsync(
            string providerCode,
            string providerBookKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SearchBookDetailsResponse?>(_details);
        }
    }
}
