using Bookshelf.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly BookshelfDbContext _dbContext;

    public UserRepository(BookshelfDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureExistsAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "userId must be greater than zero.");
        }

        var exists = await _dbContext.Users
            .AnyAsync(x => x.Id == userId, cancellationToken);
        if (exists)
        {
            return;
        }

        var login = $"user-{userId}";
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO users (id, login, created_at_utc)
            VALUES ({userId}, {login}, now())
            ON CONFLICT (id) DO NOTHING
            """,
            cancellationToken);
    }
}
