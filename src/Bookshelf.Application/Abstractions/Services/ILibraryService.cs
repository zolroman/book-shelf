using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Abstractions.Services;

public interface ILibraryService
{
    Task<LibraryResponse> ListAsync(
        long userId,
        bool includeArchived,
        string? query,
        string? providerCode,
        string? catalogState,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
