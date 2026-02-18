using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Services;

public sealed class BookSearchService : IBookSearchService
{
    public const string FantLabProviderCode = "fantlab";
    private readonly IReadOnlyDictionary<string, IMetadataProvider> _providerByCode;
    private readonly IBookRepository _bookRepository;

    public BookSearchService(
        IEnumerable<IMetadataProvider> providers,
        IBookRepository bookRepository)
    {
        _providerByCode = providers.ToDictionary(
            keySelector: x => x.ProviderCode,
            elementSelector: x => x,
            comparer: StringComparer.OrdinalIgnoreCase);
        _bookRepository = bookRepository;
    }

    public async Task<SearchBooksResponse> SearchAsync(
        string? title,
        string? author,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(FantLabProviderCode);

        var normalizedTitle = NormalizeOptional(title);
        var normalizedAuthor = NormalizeOptional(author);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var providerResult = await provider.SearchAsync(
            new MetadataSearchRequest(
                Title: normalizedTitle,
                Author: normalizedAuthor,
                Page: safePage,
                PageSize: safePageSize),
            cancellationToken);

        var mappedItems = new List<SearchBookItemDto>(providerResult.Items.Count);
        foreach (var item in providerResult.Items)
        {
            var catalogBook = await _bookRepository.GetByProviderKeyAsync(
                provider.ProviderCode,
                item.ProviderBookKey,
                cancellationToken);

            var inCatalog = catalogBook is not null;
            var catalogState = inCatalog
                ? catalogBook!.CatalogState.ToString().ToLowerInvariant()
                : "not_added";

            mappedItems.Add(new SearchBookItemDto(
                ProviderCode: provider.ProviderCode,
                ProviderBookKey: item.ProviderBookKey,
                Title: item.Title,
                Authors: item.Authors,
                Series: item.Series is null
                    ? null
                    : new SearchSeriesDto(
                        ProviderSeriesKey: item.Series.ProviderSeriesKey,
                        Title: item.Series.Title,
                        Order: item.Series.Order),
                InCatalog: inCatalog,
                CatalogState: catalogState));
        }

        return new SearchBooksResponse(
            Query: new SearchBooksQuery(Title: normalizedTitle, Author: normalizedAuthor),
            Page: safePage,
            PageSize: safePageSize,
            Total: providerResult.Total,
            Items: mappedItems);
    }

    public async Task<SearchBookDetailsResponse?> GetDetailsAsync(
        string providerCode,
        string providerBookKey,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(providerCode);
        var details = await provider.GetDetailsAsync(providerBookKey, cancellationToken);
        if (details is null)
        {
            return null;
        }

        return new SearchBookDetailsResponse(
            ProviderCode: provider.ProviderCode,
            ProviderBookKey: details.ProviderBookKey,
            Title: details.Title,
            OriginalTitle: details.OriginalTitle,
            Description: details.Description,
            PublishYear: details.PublishYear,
            CoverUrl: details.CoverUrl,
            Authors: details.Authors,
            Series: details.Series is null
                ? null
                : new SearchSeriesDto(
                    ProviderSeriesKey: details.Series.ProviderSeriesKey,
                    Title: details.Series.Title,
                    Order: details.Series.Order));
    }

    private IMetadataProvider GetProvider(string providerCode)
    {
        if (_providerByCode.TryGetValue(providerCode, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"Metadata provider '{providerCode}' is not registered.");
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var collapsed = string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(collapsed) ? null : collapsed;
    }
}
