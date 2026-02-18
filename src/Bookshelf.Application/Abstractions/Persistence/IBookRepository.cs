using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.Abstractions.Persistence;

public interface IBookRepository
{
    Task<Book?> GetByIdAsync(long bookId, CancellationToken cancellationToken = default);

    Task<Book?> GetByProviderKeyAsync(
        string providerCode,
        string providerBookKey,
        CancellationToken cancellationToken = default);

    Task AddAsync(Book book, CancellationToken cancellationToken = default);

    void Update(Book book);
}
