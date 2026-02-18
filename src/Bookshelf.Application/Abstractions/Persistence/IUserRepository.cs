namespace Bookshelf.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task EnsureExistsAsync(long userId, CancellationToken cancellationToken = default);
}
