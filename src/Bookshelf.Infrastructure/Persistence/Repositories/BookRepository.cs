using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.Repositories;

public sealed class BookRepository : IBookRepository
{
    private readonly BookshelfDbContext _dbContext;

    public BookRepository(BookshelfDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Book?> GetByIdAsync(long bookId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Books
            .Include(x => x.MediaAssets)
            .Include(x => x.BookAuthors)
                .ThenInclude(x => x.Author)
            .Include(x => x.SeriesBooks)
                .ThenInclude(x => x.Series)
            .FirstOrDefaultAsync(x => x.Id == bookId, cancellationToken);
    }

    public async Task<Book?> GetByProviderKeyAsync(
        string providerCode,
        string providerBookKey,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Books
            .Include(x => x.MediaAssets)
            .Include(x => x.BookAuthors)
                .ThenInclude(x => x.Author)
            .Include(x => x.SeriesBooks)
                .ThenInclude(x => x.Series)
            .FirstOrDefaultAsync(
                x => x.ProviderCode == providerCode && x.ProviderBookKey == providerBookKey,
                cancellationToken);
    }

    public async Task<Author?> GetAuthorByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim();
        return await _dbContext.Authors
            .FirstOrDefaultAsync(x => x.Name == normalized, cancellationToken);
    }

    public async Task AddAuthorAsync(Author author, CancellationToken cancellationToken = default)
    {
        await _dbContext.Authors.AddAsync(author, cancellationToken);
    }

    public async Task<Series?> GetSeriesByProviderKeyAsync(
        string providerCode,
        string providerSeriesKey,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Series
            .FirstOrDefaultAsync(
                x => x.ProviderCode == providerCode && x.ProviderSeriesKey == providerSeriesKey,
                cancellationToken);
    }

    public async Task AddSeriesAsync(Series series, CancellationToken cancellationToken = default)
    {
        await _dbContext.Series.AddAsync(series, cancellationToken);
    }

    public async Task<IReadOnlyList<Book>> ListLibraryAsync(
        bool includeArchived,
        string? query,
        string? providerCode,
        CatalogState? catalogState,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var booksQuery = BuildLibraryQuery(includeArchived, query, providerCode, catalogState);

        return await booksQuery
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountLibraryAsync(
        bool includeArchived,
        string? query,
        string? providerCode,
        CatalogState? catalogState,
        CancellationToken cancellationToken = default)
    {
        return BuildLibraryQuery(includeArchived, query, providerCode, catalogState)
            .CountAsync(cancellationToken);
    }

    public async Task AddAsync(Book book, CancellationToken cancellationToken = default)
    {
        await _dbContext.Books.AddAsync(book, cancellationToken);
    }

    public void Update(Book book)
    {
        _dbContext.Books.Update(book);
    }

    private IQueryable<Book> BuildLibraryQuery(
        bool includeArchived,
        string? query,
        string? providerCode,
        CatalogState? catalogState)
    {
        var booksQuery = _dbContext.Books
            .AsNoTracking()
            .Include(x => x.MediaAssets)
            .AsQueryable();

        if (!includeArchived)
        {
            booksQuery = booksQuery.Where(x => x.CatalogState == CatalogState.Library);
        }

        if (catalogState.HasValue)
        {
            var stateValue = catalogState.Value;
            booksQuery = booksQuery.Where(x => x.CatalogState == stateValue);
        }

        if (!string.IsNullOrWhiteSpace(providerCode))
        {
            var normalizedProviderCode = providerCode.Trim();
            booksQuery = booksQuery.Where(x => x.ProviderCode == normalizedProviderCode);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalizedQuery = query.Trim().ToLowerInvariant();
            booksQuery = booksQuery.Where(x =>
                x.Title.ToLower().Contains(normalizedQuery) ||
                (x.OriginalTitle != null && x.OriginalTitle.ToLower().Contains(normalizedQuery)));
        }

        return booksQuery;
    }
}
