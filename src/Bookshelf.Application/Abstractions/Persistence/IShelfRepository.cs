using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.Abstractions.Persistence;

public interface IShelfRepository
{
    Task<Shelf?> GetByIdAsync(long shelfId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Shelf>> ListByUserAsync(long userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(long userId, string shelfName, CancellationToken cancellationToken = default);

    Task AddAsync(Shelf shelf, CancellationToken cancellationToken = default);

    void Remove(Shelf shelf);
}
