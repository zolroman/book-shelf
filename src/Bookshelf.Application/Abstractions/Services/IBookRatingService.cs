using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Abstractions.Services;

public interface IBookRatingService
{
    Task<int?> GetRatingAsync(
        long userId,
        long bookId,
        CancellationToken cancellationToken = default);

    Task<BookUserRatingDto> UpsertAsync(
        long userId,
        long bookId,
        int rating,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        long userId,
        long bookId,
        CancellationToken cancellationToken = default);
}
