using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Services;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Tests;

public class LibraryServiceTests
{
    [Fact]
    public async Task ListAsync_MapsBooksAndUsesDefaultPaging()
    {
        var repository = new FakeBookRepository
        {
            Books =
            [
                CreateBook(1, CatalogState.Library, "Dune"),
                CreateBook(2, CatalogState.Archive, "Dune Messiah"),
            ],
        };

        ILibraryService service = new LibraryService(repository);

        var response = await service.ListAsync(
            userId: 10,
            includeArchived: true,
            query: null,
            providerCode: null,
            catalogState: null,
            page: 0,
            pageSize: 0);

        Assert.Equal(1, response.Page);
        Assert.Equal(20, response.PageSize);
        Assert.Equal(2, response.Total);
        Assert.Equal(2, response.Items.Count);
        Assert.Contains(response.Items, item => item.CatalogState == "library");
    }

    [Fact]
    public async Task ListAsync_InvalidCatalogState_Throws()
    {
        var repository = new FakeBookRepository();
        ILibraryService service = new LibraryService(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await service.ListAsync(
                userId: 10,
                includeArchived: true,
                query: null,
                providerCode: null,
                catalogState: "invalid",
                page: 1,
                pageSize: 20));
    }

    private static Book CreateBook(long id, CatalogState state, string title)
    {
        var book = new Book("fantlab", id.ToString(), title);
        SetProperty(book, "Id", id);
        SetProperty(book, "CatalogState", state);
        return book;
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

    private sealed class FakeBookRepository : IBookRepository
    {
        public List<Book> Books { get; set; } = [];

        public Task<Book?> GetByIdAsync(long bookId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Books.SingleOrDefault(x => x.Id == bookId));
        }

        public Task<Book?> GetByProviderKeyAsync(
            string providerCode,
            string providerBookKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Books.SingleOrDefault(x =>
                x.ProviderCode == providerCode && x.ProviderBookKey == providerBookKey));
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
            IEnumerable<Book> result = Books;
            if (!includeArchived)
            {
                result = result.Where(x => x.CatalogState == CatalogState.Library);
            }

            if (catalogState.HasValue)
            {
                result = result.Where(x => x.CatalogState == catalogState.Value);
            }

            if (!string.IsNullOrWhiteSpace(providerCode))
            {
                result = result.Where(x => x.ProviderCode == providerCode);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var normalizedQuery = query.Trim().ToLowerInvariant();
                result = result.Where(x =>
                    x.Title.ToLowerInvariant().Contains(normalizedQuery, StringComparison.Ordinal) ||
                    (!string.IsNullOrWhiteSpace(x.OriginalTitle) &&
                     x.OriginalTitle.ToLowerInvariant().Contains(normalizedQuery, StringComparison.Ordinal)));
            }

            var safePage = page < 1 ? 1 : page;
            var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            result = result
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize);

            return Task.FromResult<IReadOnlyList<Book>>(result.ToArray());
        }

        public Task<int> CountLibraryAsync(
            bool includeArchived,
            string? query,
            string? providerCode,
            CatalogState? catalogState,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<Book> result = Books;
            if (!includeArchived)
            {
                result = result.Where(x => x.CatalogState == CatalogState.Library);
            }

            if (catalogState.HasValue)
            {
                result = result.Where(x => x.CatalogState == catalogState.Value);
            }

            if (!string.IsNullOrWhiteSpace(providerCode))
            {
                result = result.Where(x => x.ProviderCode == providerCode);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var normalizedQuery = query.Trim().ToLowerInvariant();
                result = result.Where(x =>
                    x.Title.ToLowerInvariant().Contains(normalizedQuery, StringComparison.Ordinal) ||
                    (!string.IsNullOrWhiteSpace(x.OriginalTitle) &&
                     x.OriginalTitle.ToLowerInvariant().Contains(normalizedQuery, StringComparison.Ordinal)));
            }

            return Task.FromResult(result.Count());
        }

        public Task AddAsync(Book book, CancellationToken cancellationToken = default)
        {
            Books.Add(book);
            return Task.CompletedTask;
        }

        public void Update(Book book)
        {
        }
    }
}
