using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Domain.Enums;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Services;

public sealed class LibraryService : ILibraryService
{
    private readonly IBookRepository _bookRepository;

    public LibraryService(IBookRepository bookRepository)
    {
        _bookRepository = bookRepository;
    }

    public async Task<LibraryResponse> ListAsync(
        long userId,
        bool includeArchived,
        string? query,
        string? providerCode,
        string? catalogState,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        _ = userId;
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var normalizedQuery = NormalizeOptional(query);
        var normalizedProviderCode = NormalizeOptional(providerCode);
        var stateFilter = ParseCatalogState(catalogState);

        var total = await _bookRepository.CountLibraryAsync(
            includeArchived,
            normalizedQuery,
            normalizedProviderCode,
            stateFilter,
            cancellationToken);

        var books = await _bookRepository.ListLibraryAsync(
            includeArchived,
            normalizedQuery,
            normalizedProviderCode,
            stateFilter,
            safePage,
            safePageSize,
            cancellationToken);

        return new LibraryResponse(
            Page: safePage,
            PageSize: safePageSize,
            Total: total,
            IncludeArchived: includeArchived,
            Items: books.Select(x => new LibraryBookDto(
                Id: x.Id,
                ProviderCode: x.ProviderCode,
                ProviderBookKey: x.ProviderBookKey,
                Title: x.Title,
                OriginalTitle: x.OriginalTitle,
                Description: x.Description,
                PublishYear: x.PublishYear,
                LanguageCode: x.LanguageCode,
                CoverUrl: x.CoverUrl,
                CatalogState: x.CatalogState.ToString().ToLowerInvariant(),
                CreatedAtUtc: x.CreatedAtUtc,
                UpdatedAtUtc: x.UpdatedAtUtc))
            .ToArray());
    }

    private static CatalogState? ParseCatalogState(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "archive" => CatalogState.Archive,
            "library" => CatalogState.Library,
            _ => throw new ArgumentException("Invalid catalogState filter.", nameof(value)),
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
