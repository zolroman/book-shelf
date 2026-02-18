using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Services;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Tests;

public class BookSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_NormalizesInputs_AndMapsCatalogFlags()
    {
        var provider = new FakeMetadataProvider("fantlab")
        {
            SearchResult = new MetadataSearchResult(
                Total: 2,
                Items:
                [
                    new MetadataSearchItem(
                        ProviderBookKey: "123",
                        Title: "Dune",
                        Authors: ["Frank Herbert"],
                        Series: new MetadataSeriesInfo("77", "Dune Saga", 1)),
                    new MetadataSearchItem(
                        ProviderBookKey: "456",
                        Title: "Dune Messiah",
                        Authors: ["Frank Herbert"],
                        Series: null),
                ]),
        };

        var bookRepository = new FakeBookRepository();
        bookRepository.Seed(new Book("fantlab", "123", "Dune"), CatalogState.Library);

        var service = new BookSearchService([provider], bookRepository);

        var response = await service.SearchAsync(
            title: "  dune   saga  ",
            author: "  frank   herbert ",
            page: 0,
            pageSize: 999);

        Assert.NotNull(provider.LastSearchRequest);
        Assert.Equal("dune saga", provider.LastSearchRequest!.Title);
        Assert.Equal("frank herbert", provider.LastSearchRequest.Author);
        Assert.Equal(1, provider.LastSearchRequest.Page);
        Assert.Equal(20, provider.LastSearchRequest.PageSize);

        Assert.Equal("dune saga", response.Query.Title);
        Assert.Equal("frank herbert", response.Query.Author);
        Assert.Equal(1, response.Page);
        Assert.Equal(20, response.PageSize);
        Assert.Equal(2, response.Total);
        Assert.Equal(2, response.Items.Count);

        var inCatalog = Assert.Single(response.Items, x => x.ProviderBookKey == "123");
        Assert.True(inCatalog.InCatalog);
        Assert.Equal("library", inCatalog.CatalogState);
        Assert.NotNull(inCatalog.Series);

        var notAdded = Assert.Single(response.Items, x => x.ProviderBookKey == "456");
        Assert.False(notAdded.InCatalog);
        Assert.Equal("not_added", notAdded.CatalogState);
    }

    [Fact]
    public async Task SearchAsync_WithoutRegisteredFantLabProvider_Throws()
    {
        var service = new BookSearchService([new FakeMetadataProvider("other")], new FakeBookRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.SearchAsync("dune", null, 1, 20));
    }

    [Fact]
    public async Task GetDetailsAsync_MapsResponse_AndReturnsNullWhenProviderReturnsNull()
    {
        var provider = new FakeMetadataProvider("fantlab")
        {
            DetailsByKey =
            {
                ["123"] = new MetadataBookDetails(
                    ProviderBookKey: "123",
                    Title: "Dune",
                    OriginalTitle: "Dune",
                    Description: "Sci-fi classic",
                    PublishYear: 1965,
                    CoverUrl: "https://images.example/dune.jpg",
                    Authors: ["Frank Herbert"],
                    Series: new MetadataSeriesInfo("77", "Dune Saga", 1)),
            },
        };

        var service = new BookSearchService([provider], new FakeBookRepository());

        var details = await service.GetDetailsAsync("fantlab", "123");
        var missing = await service.GetDetailsAsync("fantlab", "missing");

        Assert.NotNull(details);
        Assert.Equal("fantlab", details!.ProviderCode);
        Assert.Equal("123", details.ProviderBookKey);
        Assert.Equal("Dune", details.Title);
        Assert.Equal("Dune Saga", details.Series!.Title);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetDetailsAsync_UnknownProvider_Throws()
    {
        var service = new BookSearchService([new FakeMetadataProvider("fantlab")], new FakeBookRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.GetDetailsAsync("other", "123"));
    }

    private sealed class FakeMetadataProvider : IMetadataProvider
    {
        public FakeMetadataProvider(string providerCode)
        {
            ProviderCode = providerCode;
        }

        public string ProviderCode { get; }

        public MetadataSearchRequest? LastSearchRequest { get; private set; }

        public MetadataSearchResult SearchResult { get; set; } =
            new(0, Array.Empty<MetadataSearchItem>());

        public Dictionary<string, MetadataBookDetails?> DetailsByKey { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Task<MetadataSearchResult> SearchAsync(
            MetadataSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            LastSearchRequest = request;
            return Task.FromResult(SearchResult);
        }

        public Task<MetadataBookDetails?> GetDetailsAsync(
            string providerBookKey,
            CancellationToken cancellationToken = default)
        {
            DetailsByKey.TryGetValue(providerBookKey, out var details);
            return Task.FromResult(details);
        }
    }

    private sealed class FakeBookRepository : IBookRepository
    {
        private readonly List<Book> _books = [];

        public void Seed(Book book, CatalogState state)
        {
            SetProperty(book, nameof(Book.CatalogState), state);
            _books.Add(book);
        }

        public Task<Book?> GetByIdAsync(long bookId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_books.SingleOrDefault(x => x.Id == bookId));
        }

        public Task<Book?> GetByProviderKeyAsync(
            string providerCode,
            string providerBookKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_books.SingleOrDefault(x =>
                x.ProviderCode.Equals(providerCode, StringComparison.OrdinalIgnoreCase) &&
                x.ProviderBookKey.Equals(providerBookKey, StringComparison.OrdinalIgnoreCase)));
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
            return Task.FromResult<IReadOnlyList<Book>>(Array.Empty<Book>());
        }

        public Task<int> CountLibraryAsync(
            bool includeArchived,
            string? query,
            string? providerCode,
            CatalogState? catalogState,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task AddAsync(Book book, CancellationToken cancellationToken = default)
        {
            _books.Add(book);
            return Task.CompletedTask;
        }

        public void Update(Book book)
        {
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
    }
}
