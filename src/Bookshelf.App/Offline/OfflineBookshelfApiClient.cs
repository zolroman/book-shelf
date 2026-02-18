using System.Globalization;
using System.Text.Json;
using Bookshelf.Shared.Client;
using Bookshelf.Shared.Contracts.Api;
using Microsoft.Extensions.Logging;

namespace Bookshelf.Offline;

public sealed class OfflineBookshelfApiClient : IBookshelfApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private const string QueueOperationUpsertProgress = "upsert_progress";
    private const string QueueOperationAppendHistory = "append_history";

    private readonly BookshelfApiClient _remoteClient;
    private readonly OfflineStore _store;
    private readonly UserSessionState _sessionState;
    private readonly IConnectivityState _connectivityState;
    private readonly ILogger<OfflineBookshelfApiClient> _logger;

    public OfflineBookshelfApiClient(
        BookshelfApiClient remoteClient,
        OfflineStore store,
        UserSessionState sessionState,
        IConnectivityState connectivityState,
        ILogger<OfflineBookshelfApiClient> logger)
    {
        _remoteClient = remoteClient;
        _store = store;
        _sessionState = sessionState;
        _connectivityState = connectivityState;
        _logger = logger;
    }

    public async Task<SearchBooksResponse> SearchBooksAsync(
        string? title,
        string? author,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var key = $"search:{_sessionState.UserId}:{title}:{author}:{page}:{pageSize}";
        var ttl = TimeSpan.FromHours(24);
        if (_connectivityState.IsOnline)
        {
            try
            {
                var response = await _remoteClient.SearchBooksAsync(
                    title,
                    author,
                    page,
                    pageSize,
                    cancellationToken);
                await _store.SetCacheAsync(key, response, ttl, cancellationToken);
                return response;
            }
            catch (ApiClientException exception) when (IsOfflineEquivalent(exception))
            {
                _logger.LogWarning(exception, "Search request failed. Falling back to cache.");
            }
        }

        var cached = await _store.GetCacheAsync<SearchBooksResponse>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        throw CreateNetworkRequiredException();
    }

    public async Task<SearchBookDetailsResponse> GetBookDetailsAsync(
        string providerCode,
        string providerBookKey,
        CancellationToken cancellationToken = default)
    {
        var key = $"details:{providerCode}:{providerBookKey}";
        var ttl = TimeSpan.FromDays(7);
        if (_connectivityState.IsOnline)
        {
            try
            {
                var response = await _remoteClient.GetBookDetailsAsync(providerCode, providerBookKey, cancellationToken);
                await _store.SetCacheAsync(key, response, ttl, cancellationToken);
                return response;
            }
            catch (ApiClientException exception) when (IsOfflineEquivalent(exception))
            {
                _logger.LogWarning(exception, "Details request failed. Falling back to cache.");
            }
        }

        var cached = await _store.GetCacheAsync<SearchBookDetailsResponse>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        throw CreateNetworkRequiredException();
    }

    public Task<DownloadCandidatesResponse> GetCandidatesAsync(
        string providerCode,
        string providerBookKey,
        string mediaType,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!_connectivityState.IsOnline)
        {
            throw CreateNetworkRequiredException();
        }

        return _remoteClient.GetCandidatesAsync(
            providerCode,
            providerBookKey,
            mediaType,
            page,
            pageSize,
            cancellationToken);
    }

    public Task<AddAndDownloadResponse> AddAndDownloadAsync(
        string providerCode,
        string providerBookKey,
        string mediaType,
        string candidateId,
        CancellationToken cancellationToken = default)
    {
        if (!_connectivityState.IsOnline)
        {
            throw CreateNetworkRequiredException();
        }

        return _remoteClient.AddAndDownloadAsync(
            providerCode,
            providerBookKey,
            mediaType,
            candidateId,
            cancellationToken);
    }

    public async Task<DownloadJobsResponse> ListDownloadJobsAsync(
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var key = $"jobs:{_sessionState.UserId}:{status}:{page}:{pageSize}";
        if (_connectivityState.IsOnline)
        {
            try
            {
                var response = await _remoteClient.ListDownloadJobsAsync(status, page, pageSize, cancellationToken);
                await _store.SetCacheAsync(key, response, TimeSpan.FromMinutes(10), cancellationToken);
                await UpdateMediaIndexFromJobsAsync(response.Items, cancellationToken);
                return response;
            }
            catch (ApiClientException exception) when (IsOfflineEquivalent(exception))
            {
                _logger.LogWarning(exception, "Download jobs request failed. Falling back to cache.");
            }
        }

        var cached = await _store.GetCacheAsync<DownloadJobsResponse>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        return new DownloadJobsResponse(page, pageSize, 0, Array.Empty<DownloadJobDto>());
    }

    public Task<DownloadJobDto> GetDownloadJobAsync(long jobId, CancellationToken cancellationToken = default)
    {
        if (!_connectivityState.IsOnline)
        {
            throw CreateNetworkRequiredException();
        }

        return _remoteClient.GetDownloadJobAsync(jobId, cancellationToken);
    }

    public Task<DownloadJobDto> CancelDownloadJobAsync(long jobId, CancellationToken cancellationToken = default)
    {
        if (!_connectivityState.IsOnline)
        {
            throw CreateNetworkRequiredException();
        }

        return _remoteClient.CancelDownloadJobAsync(jobId, cancellationToken);
    }

    public async Task<LibraryResponse> GetLibraryAsync(
        bool includeArchived,
        string? query,
        string? providerCode,
        string? catalogState,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var key = $"library:{_sessionState.UserId}:{includeArchived}:{query}:{providerCode}:{catalogState}:{page}:{pageSize}";
        if (_connectivityState.IsOnline)
        {
            try
            {
                var response = await _remoteClient.GetLibraryAsync(
                    includeArchived,
                    query,
                    providerCode,
                    catalogState,
                    page,
                    pageSize,
                    cancellationToken);
                await _store.SetCacheAsync(key, response, TimeSpan.FromDays(7), cancellationToken);
                return response;
            }
            catch (ApiClientException exception) when (IsOfflineEquivalent(exception))
            {
                _logger.LogWarning(exception, "Library request failed. Falling back to cache.");
            }
        }

        var cached = await _store.GetCacheAsync<LibraryResponse>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        throw CreateNetworkRequiredException();
    }

    public async Task<ShelvesResponse> GetShelvesAsync(CancellationToken cancellationToken = default)
    {
        var key = $"shelves:{_sessionState.UserId}";
        if (_connectivityState.IsOnline)
        {
            try
            {
                var response = await _remoteClient.GetShelvesAsync(cancellationToken);
                await _store.SetCacheAsync(key, response, TimeSpan.FromDays(7), cancellationToken);
                return response;
            }
            catch (ApiClientException exception) when (IsOfflineEquivalent(exception))
            {
                _logger.LogWarning(exception, "Shelves request failed. Falling back to cache.");
            }
        }

        var cached = await _store.GetCacheAsync<ShelvesResponse>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        return new ShelvesResponse(Array.Empty<ShelfDto>());
    }

    public Task<CreateShelfResponse> CreateShelfAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!_connectivityState.IsOnline)
        {
            throw CreateNetworkRequiredException();
        }

        return _remoteClient.CreateShelfAsync(name, cancellationToken);
    }

    public Task<AddBookToShelfResponse> AddBookToShelfAsync(
        long shelfId,
        long bookId,
        CancellationToken cancellationToken = default)
    {
        if (!_connectivityState.IsOnline)
        {
            throw CreateNetworkRequiredException();
        }

        return _remoteClient.AddBookToShelfAsync(shelfId, bookId, cancellationToken);
    }

    public Task RemoveBookFromShelfAsync(
        long shelfId,
        long bookId,
        CancellationToken cancellationToken = default)
    {
        if (!_connectivityState.IsOnline)
        {
            throw CreateNetworkRequiredException();
        }

        return _remoteClient.RemoveBookFromShelfAsync(shelfId, bookId, cancellationToken);
    }

    public async Task<ProgressSnapshotDto> UpsertProgressAsync(
        UpsertProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = request.UpdatedAtUtc ?? DateTimeOffset.UtcNow;
        var localSnapshot = new ProgressSnapshotDto(
            UserId: _sessionState.UserId,
            BookId: request.BookId,
            MediaType: request.MediaType.Trim().ToLowerInvariant(),
            PositionRef: request.PositionRef.Trim(),
            ProgressPercent: request.ProgressPercent,
            UpdatedAtUtc: nowUtc);
        await _store.UpsertProgressAsync(localSnapshot, cancellationToken);

        if (_connectivityState.IsOnline)
        {
            try
            {
                var remoteSnapshot = await _remoteClient.UpsertProgressAsync(request, cancellationToken);
                await _store.UpsertProgressAsync(remoteSnapshot, cancellationToken);
                return remoteSnapshot;
            }
            catch (ApiClientException exception) when (IsRetryableForQueue(exception))
            {
                _logger.LogWarning(exception, "Progress push failed. Queued for later sync.");
            }
        }

        await EnqueueAsync(
            QueueOperationUpsertProgress,
            JsonSerializer.Serialize(request, JsonOptions),
            cancellationToken);
        return localSnapshot;
    }

    public async Task<ProgressSnapshotsResponse> ListProgressAsync(
        long? bookId,
        string? mediaType,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (_connectivityState.IsOnline)
        {
            try
            {
                var remote = await _remoteClient.ListProgressAsync(
                    bookId,
                    mediaType,
                    page,
                    pageSize,
                    cancellationToken);
                foreach (var snapshot in remote.Items)
                {
                    await _store.UpsertProgressAsync(snapshot, cancellationToken);
                }

                return remote;
            }
            catch (ApiClientException exception) when (IsOfflineEquivalent(exception))
            {
                _logger.LogWarning(exception, "Progress list request failed. Falling back to local store.");
            }
        }

        return await _store.ListProgressAsync(
            _sessionState.UserId,
            bookId,
            mediaType,
            page,
            pageSize,
            cancellationToken);
    }

    public async Task<AppendHistoryEventsResponse> AppendHistoryEventsAsync(
        AppendHistoryEventsRequest request,
        CancellationToken cancellationToken = default)
    {
        var local = await _store.AppendHistoryEventsAsync(_sessionState.UserId, request.Items, cancellationToken);

        if (_connectivityState.IsOnline)
        {
            try
            {
                return await _remoteClient.AppendHistoryEventsAsync(request, cancellationToken);
            }
            catch (ApiClientException exception) when (IsRetryableForQueue(exception))
            {
                _logger.LogWarning(exception, "History push failed. Queued for later sync.");
            }
        }

        await EnqueueAsync(
            QueueOperationAppendHistory,
            JsonSerializer.Serialize(request, JsonOptions),
            cancellationToken);
        return local;
    }

    public async Task<HistoryEventsResponse> ListHistoryEventsAsync(
        long? bookId,
        string? mediaType,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (_connectivityState.IsOnline)
        {
            try
            {
                var remote = await _remoteClient.ListHistoryEventsAsync(
                    bookId,
                    mediaType,
                    page,
                    pageSize,
                    cancellationToken);
                await _store.AppendHistoryEventsAsync(
                    _sessionState.UserId,
                    remote.Items.Select(item => new HistoryEventWriteDto(
                        BookId: item.BookId,
                        MediaType: item.MediaType,
                        EventType: item.EventType,
                        PositionRef: item.PositionRef,
                        EventAtUtc: item.EventAtUtc)).ToArray(),
                    cancellationToken);
                return remote;
            }
            catch (ApiClientException exception) when (IsOfflineEquivalent(exception))
            {
                _logger.LogWarning(exception, "History list request failed. Falling back to local store.");
            }
        }

        return await _store.ListHistoryEventsAsync(
            _sessionState.UserId,
            bookId,
            mediaType,
            page,
            pageSize,
            cancellationToken);
    }

    private async Task EnqueueAsync(
        string operationType,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        await _store.EnqueueOperationAsync(operationType, payloadJson, cancellationToken);
    }

    private async Task UpdateMediaIndexFromJobsAsync(
        IReadOnlyList<DownloadJobDto> jobs,
        CancellationToken cancellationToken)
    {
        foreach (var job in jobs)
        {
            if (!job.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var localPath = $"downloads/{job.ExternalJobId ?? job.Id.ToString(CultureInfo.InvariantCulture)}";
            var entry = new LocalMediaEntry(
                UserId: _sessionState.UserId,
                BookId: job.BookId,
                MediaType: job.MediaType,
                LocalPath: localPath,
                IsAvailable: true,
                UpdatedAtUtc: job.CompletedAtUtc ?? job.UpdatedAtUtc);
            await _store.UpsertMediaEntryAsync(entry, cancellationToken);
        }
    }

    private static bool IsOfflineEquivalent(ApiClientException exception)
    {
        return exception.StatusCode >= 500 ||
               exception.StatusCode == 503 ||
               exception.Code.Equals("NETWORK_REQUIRED", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRetryableForQueue(ApiClientException exception)
    {
        return exception.StatusCode >= 500 ||
               exception.StatusCode == 503 ||
               exception.Code.EndsWith("_UNAVAILABLE", StringComparison.OrdinalIgnoreCase) ||
               exception.Code.Equals("NETWORK_REQUIRED", StringComparison.OrdinalIgnoreCase);
    }

    private static ApiClientException CreateNetworkRequiredException()
    {
        return new ApiClientException(
            statusCode: 422,
            code: "NETWORK_REQUIRED",
            message: "Network connection is required for this action.");
    }
}
