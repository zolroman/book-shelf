using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Abstractions.Services;

public interface IShelfService
{
    Task<ShelvesResponse> ListAsync(
        long userId,
        CancellationToken cancellationToken = default);

    Task<ShelfDto?> CreateAsync(
        long userId,
        string name,
        CancellationToken cancellationToken = default);

    Task<ShelfAddBookResult> AddBookAsync(
        long shelfId,
        long userId,
        long bookId,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveBookAsync(
        long shelfId,
        long userId,
        long bookId,
        CancellationToken cancellationToken = default);
}

public sealed record ShelfAddBookResult(
    ShelfAddBookResultStatus Status,
    ShelfDto? Shelf);

public enum ShelfAddBookResultStatus
{
    Success = 0,
    NotFound = 1,
    AlreadyExists = 2,
}
