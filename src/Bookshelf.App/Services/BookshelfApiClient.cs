using System.Net.Http.Json;
using Bookshelf.Shared.Contracts.Books;
using Bookshelf.Shared.Contracts.History;
using Bookshelf.Shared.Contracts.Library;
using Bookshelf.Shared.Contracts.Progress;
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

    public async Task<BookDetailsDto?> GetBookDetailsAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"book-details-{bookId}";
        try
        {
            var response = await _httpClient.GetFromJsonAsync<BookDetailsDto>($"api/books/{bookId}", cancellationToken);
            if (response is not null)
            {
                await _offlineCacheService.SaveAsync(cacheKey, response, cancellationToken);
                return response;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Book details request failed. Returning offline data.");
        }

        return await _offlineCacheService.LoadAsync<BookDetailsDto>(cacheKey, cancellationToken);
    }

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

    public async Task<ProgressSnapshotDto?> GetProgressAsync(
        int userId,
        int bookId,
        string formatType,
        CancellationToken cancellationToken = default)
    {
        var normalizedFormat = formatType.ToLowerInvariant();
        var cacheKey = $"progress-{userId}-{bookId}-{normalizedFormat}";
        try
        {
            var response = await _httpClient.GetAsync(
                $"api/progress?userId={userId}&bookId={bookId}&formatType={Uri.EscapeDataString(normalizedFormat)}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return await _offlineCacheService.LoadAsync<ProgressSnapshotDto>(cacheKey, cancellationToken);
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<ProgressSnapshotDto>(cancellationToken: cancellationToken);
            if (payload is not null)
            {
                await _offlineCacheService.SaveAsync(cacheKey, payload, cancellationToken);
            }

            return payload;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Get progress request failed. Returning local checkpoint.");
            return await _offlineCacheService.LoadAsync<ProgressSnapshotDto>(cacheKey, cancellationToken);
        }
    }

    public async Task<ProgressSnapshotDto?> UpsertProgressAsync(
        UpsertProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedFormat = request.FormatType.ToLowerInvariant();
        var cacheKey = $"progress-{request.UserId}-{request.BookId}-{normalizedFormat}";
        try
        {
            var response = await _httpClient.PutAsJsonAsync("api/progress", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ProgressSnapshotDto>(cancellationToken: cancellationToken);
            if (payload is not null)
            {
                await _offlineCacheService.SaveAsync(cacheKey, payload, cancellationToken);
            }

            return payload;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Upsert progress request failed. Saving local shadow state.");

            var localShadow = new ProgressSnapshotDto(
                Id: 0,
                UserId: request.UserId,
                BookId: request.BookId,
                FormatType: normalizedFormat,
                PositionRef: request.PositionRef,
                ProgressPercent: request.ProgressPercent,
                UpdatedAtUtc: DateTime.UtcNow);

            await _offlineCacheService.SaveAsync(cacheKey, localShadow, cancellationToken);
            return localShadow;
        }
    }

    public async Task<bool> AddHistoryEventAsync(
        AddHistoryEventRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/history", request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "History event request failed.");
            return false;
        }
    }
}
