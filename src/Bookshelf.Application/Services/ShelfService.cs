using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Domain.Entities;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Services;

public sealed class ShelfService : IShelfService
{
    private readonly IShelfRepository _shelfRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ShelfService(
        IShelfRepository shelfRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _shelfRepository = shelfRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ShelvesResponse> ListAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var shelves = await _shelfRepository.ListByUserAsync(userId, cancellationToken);
        return new ShelvesResponse(shelves.Select(Map).ToArray());
    }

    public async Task<ShelfDto?> CreateAsync(
        long userId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();
        var existingShelves = await _shelfRepository.ListByUserAsync(userId, cancellationToken);
        if (existingShelves.Any(x => x.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        await _userRepository.EnsureExistsAsync(userId, cancellationToken);

        var shelf = new Shelf(userId, normalizedName);
        await _shelfRepository.AddAsync(shelf, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(shelf);
    }

    public async Task<ShelfAddBookResult> AddBookAsync(
        long shelfId,
        long userId,
        long bookId,
        CancellationToken cancellationToken = default)
    {
        var shelf = await _shelfRepository.GetByIdAsync(shelfId, cancellationToken);
        if (shelf is null || shelf.UserId != userId)
        {
            return new ShelfAddBookResult(ShelfAddBookResultStatus.NotFound, null);
        }

        if (shelf.ContainsBook(bookId))
        {
            return new ShelfAddBookResult(ShelfAddBookResultStatus.AlreadyExists, null);
        }

        shelf.AddBook(bookId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ShelfAddBookResult(ShelfAddBookResultStatus.Success, Map(shelf));
    }

    public async Task<bool> RemoveBookAsync(
        long shelfId,
        long userId,
        long bookId,
        CancellationToken cancellationToken = default)
    {
        var shelf = await _shelfRepository.GetByIdAsync(shelfId, cancellationToken);
        if (shelf is null || shelf.UserId != userId)
        {
            return false;
        }

        var relation = shelf.ShelfBooks.FirstOrDefault(x => x.BookId == bookId);
        if (relation is not null)
        {
            shelf.ShelfBooks.Remove(relation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static ShelfDto Map(Shelf shelf)
    {
        return new ShelfDto(
            Id: shelf.Id,
            UserId: shelf.UserId,
            Name: shelf.Name,
            CreatedAtUtc: shelf.CreatedAtUtc,
            BookIds: shelf.ShelfBooks
                .Select(x => x.BookId)
                .OrderBy(x => x)
                .ToArray());
    }
}
