using Bookshelf.Application.Abstractions.Persistence;

namespace Bookshelf.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly BookshelfDbContext _dbContext;

    public EfUnitOfWork(BookshelfDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
