using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.Abstractions.Persistence;

public interface IBookRatingRepository
{
    Task<BookRating?> GetAsync(
        long userId,
        long bookId,
        CancellationToken cancellationToken = default);

    Task AddAsync(BookRating rating, CancellationToken cancellationToken = default);

    void Update(BookRating rating);

    void Remove(BookRating rating);
}
