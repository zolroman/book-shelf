using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Abstractions.Persistence;

public interface IBookRepository
{
    Task<Book?> GetByIdAsync(long bookId, CancellationToken cancellationToken = default);

    Task<Book?> GetByProviderKeyAsync(
        string providerCode,
        string providerBookKey,
        CancellationToken cancellationToken = default);

    Task<Author?> GetAuthorByNameAsync(string name, CancellationToken cancellationToken = default);

    Task AddAuthorAsync(Author author, CancellationToken cancellationToken = default);

    Task<Series?> GetSeriesByProviderKeyAsync(
        string providerCode,
        string providerSeriesKey,
        CancellationToken cancellationToken = default);

    Task AddSeriesAsync(Series series, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Book>> ListLibraryAsync(
        bool includeArchived,
        string? query,
        string? providerCode,
        CatalogState? catalogState,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountLibraryAsync(
        bool includeArchived,
        string? query,
        string? providerCode,
        CatalogState? catalogState,
        CancellationToken cancellationToken = default);

    Task AddAsync(Book book, CancellationToken cancellationToken = default);

    void Update(Book book);
}
