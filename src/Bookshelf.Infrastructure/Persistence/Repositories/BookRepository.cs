using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Domain.Entities;
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

    public async Task AddAsync(Book book, CancellationToken cancellationToken = default)
    {
        await _dbContext.Books.AddAsync(book, cancellationToken);
    }

    public void Update(Book book)
    {
        _dbContext.Books.Update(book);
    }
}
