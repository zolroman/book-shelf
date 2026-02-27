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
        CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(FantLabProviderCode);

        var normalizedTitle = NormalizeOptional(title);
        var normalizedAuthor = NormalizeOptional(author);
        var safePage = page < 1 ? 1 : page;

        var providerResult = await provider.SearchAsync(
            new MetadataSearchRequest(
                Title: normalizedTitle,
                Author: normalizedAuthor,
                Page: safePage),
            cancellationToken);

        var mappedItems = new List<SearchBookItemDto>(providerResult.Items.Count);
        foreach (var item in providerResult.Items)
        {
            var itemKind = NormalizeItemKind(item.Kind);
            var isSeriesItem = itemKind.Equals(SearchItemKinds.Series, StringComparison.Ordinal);

            var inCatalog = false;
            var catalogState = "not_added";
            if (!isSeriesItem)
            {
                var catalogBook = await _bookRepository.GetByProviderKeyAsync(
                    provider.ProviderCode,
                    item.ProviderBookKey,
                    cancellationToken);

                inCatalog = catalogBook is not null;
                catalogState = inCatalog
                    ? catalogBook!.CatalogState.ToString().ToLowerInvariant()
                    : "not_added";
            }

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
                CatalogState: catalogState,
                Kind: itemKind,
                ProviderSeriesKey: isSeriesItem
                    ? NormalizeOptional(item.ProviderSeriesKey) ?? item.ProviderBookKey
                    : null));
        }

        return new SearchBooksResponse(
            Query: new SearchBooksQuery(Title: normalizedTitle, Author: normalizedAuthor),
            Page: safePage,
            PageSize: 20,
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

    public async Task<SearchSeriesDetailsResponse?> GetSeriesDetailsAsync(
        string providerCode,
        string providerSeriesKey,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(providerCode);
        var normalizedSeriesKey = NormalizeOptional(providerSeriesKey);
        if (string.IsNullOrWhiteSpace(normalizedSeriesKey))
        {
            return null;
        }

        var details = await provider.GetSeriesDetailsAsync(normalizedSeriesKey, cancellationToken);
        if (details is null)
        {
            return null;
        }

        var mappedItems = details.Items
            .OrderBy(x => x.Order)
            .Select((item, index) => new SearchSeriesBookItemDto(
                Order: item.Order > 0 ? item.Order : index + 1,
                ProviderCode: provider.ProviderCode,
                ProviderBookKey: item.ProviderBookKey,
                Title: item.Title,
                Authors: item.Authors,
                PublishYear: item.PublishYear))
            .ToArray();

        if (mappedItems.Length == 0)
        {
            return null;
        }

        return new SearchSeriesDetailsResponse(
            ProviderCode: provider.ProviderCode,
            ProviderSeriesKey: details.ProviderSeriesKey,
            Title: details.Title,
            Items: mappedItems);
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

    private static string NormalizeItemKind(string? value)
    {
        if (value?.Equals(SearchItemKinds.Series, StringComparison.OrdinalIgnoreCase) == true)
        {
            return SearchItemKinds.Series;
        }

        return SearchItemKinds.Book;
    }
}
