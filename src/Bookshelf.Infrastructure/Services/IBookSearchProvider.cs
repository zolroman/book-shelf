using Bookshelf.Domain.Entities;

namespace Bookshelf.Infrastructure.Services;

public interface IBookSearchProvider
{
    Task<IReadOnlyList<Book>> SearchAsync(string query, CancellationToken cancellationToken);
}
