using System.Net.Http.Json;
using Bookshelf.App.Models;
using Bookshelf.Shared.Contracts.Assets;
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
    IOfflineStateStore offlineStateStore,
    ILogger<BookshelfApiClient> logger) : IBookshelfApiClient, IRemoteSyncApiClient
{
    public async Task<BookDetailsDto?> GetBookDetailsAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"book-details-{bookId}";
        try
        {
            var response = await httpClient.GetFromJsonAsync<BookDetailsDto>($"api/books/{bookId}", cancellationToken);
            if (response is not null)
            {
                await offlineCacheService.SaveAsync(cacheKey, response, cancellationToken);
                return response;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Book details request failed. Returning offline data.");
        }

        return await offlineCacheService.LoadAsync<BookDetailsDto>(cacheKey, cancellationToken);
    }

    public async Task<IReadOnlyList<LibraryBookDto>> GetLibraryAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"library-{userId}";

        try
        {
            var response = await httpClient.GetFromJsonAsync<List<LibraryBookDto>>(
                $"api/library?userId={userId}",
                cancellationToken);
            if (response is not null)
            {
                await offlineCacheService.SaveAsync(cacheKey, response, cancellationToken);
                return response;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Library request failed. Returning offline data.");
        }

        return await offlineCacheService.LoadAsync<List<LibraryBookDto>>(cacheKey, cancellationToken) ?? [];
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
            var response = await httpClient.GetFromJsonAsync<SearchResultDto>(
                $"api/search?query={Uri.EscapeDataString(trimmedQuery)}",
                cancellationToken);
            if (response is not null)
            {
                await offlineCacheService.SaveAsync(cacheKey, response.Items, cancellationToken);
                return response.Items;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Search request failed. Returning offline cache.");
        }

        return await offlineCacheService.LoadAsync<List<BookSummaryDto>>(cacheKey, cancellationToken) ?? [];
    }

    public async Task<bool> AddToLibraryAsync(int userId, int bookId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(
                "api/library",
                new AddLibraryItemRequest(userId, bookId),
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Add to library request failed.");
            return false;
        }
    }

    public async Task<IReadOnlyList<LocalAssetDto>> GetAssetsAsync(int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<List<LocalAssetDto>>(
                $"api/assets?userId={userId}",
                cancellationToken);

            if (response is not null)
            {
                foreach (var asset in response)
                {
                    await offlineStateStore.UpsertLocalAssetAsync(ToLocalAssetRecord(asset), cancellationToken);
                }

                return response;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Assets request failed. Returning local index.");
        }

        var localAssets = await offlineStateStore.GetLocalAssetsAsync(userId, cancellationToken);
        return localAssets.Select(ToLocalAssetDto).ToList();
    }

    public async Task<LocalAssetDto?> UpsertLocalAssetAsync(
        UpsertLocalAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync("api/assets", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<LocalAssetDto>(cancellationToken: cancellationToken);
            if (payload is not null)
            {
                await offlineStateStore.UpsertLocalAssetAsync(ToLocalAssetRecord(payload), cancellationToken);
                return payload;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Upsert asset request failed. Updating local index only.");
        }

        var fallback = new LocalAssetDto(
            Id: 0,
            UserId: request.UserId,
            BookFormatId: request.BookFormatId,
            LocalPath: request.LocalPath,
            FileSizeBytes: request.FileSizeBytes,
            DownloadedAtUtc: DateTime.UtcNow,
            DeletedAtUtc: null);

        await offlineStateStore.UpsertLocalAssetAsync(ToLocalAssetRecord(fallback), cancellationToken);
        return fallback;
    }

    public async Task<bool> MarkAssetDeletedAsync(
        int userId,
        int bookFormatId,
        CancellationToken cancellationToken = default)
    {
        var remoteSucceeded = false;
        try
        {
            var response = await httpClient.DeleteAsync(
                $"api/assets/{bookFormatId}?userId={userId}",
                cancellationToken);

            remoteSucceeded = response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Delete asset request failed. Marking local index as deleted.");
        }

        await offlineStateStore.MarkLocalAssetDeletedAsync(userId, bookFormatId, DateTime.UtcNow, cancellationToken);
        return remoteSucceeded;
    }

    public async Task<IReadOnlyList<HistoryEventDto>> GetHistoryAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"history-{userId}";

        try
        {
            var response = await httpClient.GetFromJsonAsync<List<HistoryEventDto>>(
                $"api/history?userId={userId}",
                cancellationToken);
            if (response is not null)
            {
                await offlineCacheService.SaveAsync(cacheKey, response, cancellationToken);
                return response;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "History request failed. Returning offline data.");
        }

        return await offlineCacheService.LoadAsync<List<HistoryEventDto>>(cacheKey, cancellationToken) ?? [];
    }

    public async Task<ProgressSnapshotDto?> GetProgressAsync(
        int userId,
        int bookId,
        string formatType,
        CancellationToken cancellationToken = default)
    {
        var normalizedFormat = formatType.ToLowerInvariant();
        var cacheKey = $"progress-{userId}-{bookId}-{normalizedFormat}";

        var remote = await GetProgressRemoteAsync(userId, bookId, normalizedFormat, cancellationToken);
        if (remote is not null)
        {
            return remote;
        }

        return await offlineCacheService.LoadAsync<ProgressSnapshotDto>(cacheKey, cancellationToken);
    }

    public async Task<ProgressSnapshotDto?> GetProgressRemoteAsync(
        int userId,
        int bookId,
        string formatType,
        CancellationToken cancellationToken = default)
    {
        var normalizedFormat = formatType.ToLowerInvariant();
        var cacheKey = $"progress-{userId}-{bookId}-{normalizedFormat}";
        try
        {
            var response = await httpClient.GetAsync(
                $"api/progress?userId={userId}&bookId={bookId}&formatType={Uri.EscapeDataString(normalizedFormat)}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<ProgressSnapshotDto>(cancellationToken: cancellationToken);
            if (payload is not null)
            {
                await offlineCacheService.SaveAsync(cacheKey, payload, cancellationToken);
            }

            return payload;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Get progress remote request failed.");
            return null;
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
            var response = await httpClient.PutAsJsonAsync("api/progress", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ProgressSnapshotDto>(cancellationToken: cancellationToken);
            if (payload is not null)
            {
                await offlineCacheService.SaveAsync(cacheKey, payload, cancellationToken);
            }

            return payload;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Upsert progress request failed. Saving local shadow state.");

            var localShadow = new ProgressSnapshotDto(
                Id: 0,
                UserId: request.UserId,
                BookId: request.BookId,
                FormatType: normalizedFormat,
                PositionRef: request.PositionRef,
                ProgressPercent: request.ProgressPercent,
                UpdatedAtUtc: DateTime.UtcNow);

            await offlineCacheService.SaveAsync(cacheKey, localShadow, cancellationToken);
            return localShadow;
        }
    }

    public async Task<bool> UpsertProgressRemoteAsync(
        UpsertProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedFormat = request.FormatType.ToLowerInvariant();
        var cacheKey = $"progress-{request.UserId}-{request.BookId}-{normalizedFormat}";
        try
        {
            var response = await httpClient.PutAsJsonAsync("api/progress", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var payload = await response.Content.ReadFromJsonAsync<ProgressSnapshotDto>(cancellationToken: cancellationToken);
            if (payload is not null)
            {
                await offlineCacheService.SaveAsync(cacheKey, payload, cancellationToken);
            }

            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Upsert progress remote request failed.");
            return false;
        }
    }

    public async Task<bool> AddHistoryEventAsync(
        AddHistoryEventRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/history", request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "History event request failed.");
            return false;
        }
    }

    public async Task<bool> AddHistoryEventRemoteAsync(
        AddHistoryEventRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/history", request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Add history event remote request failed.");
            return false;
        }
    }

    private static LocalAssetIndexRecord ToLocalAssetRecord(LocalAssetDto asset)
    {
        return new LocalAssetIndexRecord(
            UserId: asset.UserId,
            BookFormatId: asset.BookFormatId,
            LocalPath: asset.LocalPath,
            FileSizeBytes: asset.FileSizeBytes,
            DownloadedAtUtc: asset.DownloadedAtUtc,
            DeletedAtUtc: asset.DeletedAtUtc);
    }

    private static LocalAssetDto ToLocalAssetDto(LocalAssetIndexRecord record)
    {
        return new LocalAssetDto(
            Id: 0,
            UserId: record.UserId,
            BookFormatId: record.BookFormatId,
            LocalPath: record.LocalPath,
            FileSizeBytes: record.FileSizeBytes,
            DownloadedAtUtc: record.DownloadedAtUtc,
            DeletedAtUtc: record.DeletedAtUtc);
    }
}
