using System.Net.Http.Json;
using Bookshelf.Shared.Contracts.Books;
using Bookshelf.Shared.Contracts.History;
using Bookshelf.Shared.Contracts.Library;
using Bookshelf.Shared.Contracts.Search;
using Microsoft.Extensions.Logging;

namespace Bookshelf.App.Services;

public sealed class BookshelfApiClient(
    HttpClient httpClient,
    IOfflineCacheService offlineCacheService,
    ILogger<BookshelfApiClient> logger) : IBookshelfApiClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IOfflineCacheService _offlineCacheService = offlineCacheService;
    private readonly ILogger<BookshelfApiClient> _logger = logger;

    public async Task<IReadOnlyList<LibraryBookDto>> GetLibraryAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"library-{userId}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<LibraryBookDto>>(
                $"api/library?userId={userId}",
                cancellationToken);
            if (response is not null)
            {
                await _offlineCacheService.SaveAsync(cacheKey, response, cancellationToken);
                return response;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Library request failed. Returning offline data.");
        }

        return await _offlineCacheService.LoadAsync<List<LibraryBookDto>>(cacheKey, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<BookSummaryDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var trimmedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            return [];
        }

        var cacheKey = $"search-{trimmedQuery.ToLowerInvariant()}";
        try
        {
            var response = await _httpClient.GetFromJsonAsync<SearchResultDto>(
                $"api/search?query={Uri.EscapeDataString(trimmedQuery)}",
                cancellationToken);
            if (response is not null)
            {
                await _offlineCacheService.SaveAsync(cacheKey, response.Items, cancellationToken);
                return response.Items;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Search request failed. Returning offline cache.");
        }

        return await _offlineCacheService.LoadAsync<List<BookSummaryDto>>(cacheKey, cancellationToken) ?? [];
    }

    public async Task<bool> AddToLibraryAsync(int userId, int bookId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/library",
                new AddLibraryItemRequest(userId, bookId),
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Add to library request failed.");
            return false;
        }
    }

    public async Task<IReadOnlyList<HistoryEventDto>> GetHistoryAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"history-{userId}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<HistoryEventDto>>(
                $"api/history?userId={userId}",
                cancellationToken);
            if (response is not null)
            {
                await _offlineCacheService.SaveAsync(cacheKey, response, cancellationToken);
                return response;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "History request failed. Returning offline data.");
        }

        return await _offlineCacheService.LoadAsync<List<HistoryEventDto>>(cacheKey, cancellationToken) ?? [];
    }
}
