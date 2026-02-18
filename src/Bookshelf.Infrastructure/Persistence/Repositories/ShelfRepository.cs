using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.Repositories;

public sealed class ShelfRepository : IShelfRepository
{
    private readonly BookshelfDbContext _dbContext;

    public ShelfRepository(BookshelfDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Shelf?> GetByIdAsync(long shelfId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Shelves
            .Include(x => x.ShelfBooks)
            .FirstOrDefaultAsync(x => x.Id == shelfId, cancellationToken);
    }

    public async Task<IReadOnlyList<Shelf>> ListByUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Shelves
            .Where(x => x.UserId == userId)
            .Include(x => x.ShelfBooks)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(long userId, string shelfName, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Shelves
            .AnyAsync(
                x => x.UserId == userId && x.Name == shelfName,
                cancellationToken);
    }

    public async Task AddAsync(Shelf shelf, CancellationToken cancellationToken = default)
    {
        await _dbContext.Shelves.AddAsync(shelf, cancellationToken);
    }

    public void Remove(Shelf shelf)
    {
        _dbContext.Shelves.Remove(shelf);
    }
}
