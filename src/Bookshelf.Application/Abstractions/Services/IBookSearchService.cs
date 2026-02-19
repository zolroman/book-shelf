using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Abstractions.Services;

public interface IBookSearchService
{
    Task<SearchBooksResponse> SearchAsync(
        string? title,
        string? author,
        int page,
        CancellationToken cancellationToken = default);

    Task<SearchBookDetailsResponse?> GetDetailsAsync(
        string providerCode,
        string providerBookKey,
        CancellationToken cancellationToken = default);
}
