using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.Repositories;

public sealed class BookRatingRepository : IBookRatingRepository
{
    private readonly BookshelfDbContext _dbContext;

    public BookRatingRepository(BookshelfDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<BookRating?> GetAsync(
        long userId,
        long bookId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.BookRatings
            .FirstOrDefaultAsync(x => x.UserId == userId && x.BookId == bookId, cancellationToken);
    }

    public Task AddAsync(BookRating rating, CancellationToken cancellationToken = default)
    {
        return _dbContext.BookRatings.AddAsync(rating, cancellationToken).AsTask();
    }

    public void Update(BookRating rating)
    {
        _dbContext.BookRatings.Update(rating);
    }

    public void Remove(BookRating rating)
    {
        _dbContext.BookRatings.Remove(rating);
    }
}
