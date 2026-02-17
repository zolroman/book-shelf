using Bookshelf.Domain.Entities;

namespace Bookshelf.Infrastructure.Services;

public sealed class InMemoryBookSearchProvider(IBookshelfRepository repository) : IBookSearchProvider
{
    private readonly IBookshelfRepository _repository = repository;

    public Task<IReadOnlyList<Book>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<Book>>([]);
        }

        return _repository.GetBooksAsync(query, null, cancellationToken);
    }
}
