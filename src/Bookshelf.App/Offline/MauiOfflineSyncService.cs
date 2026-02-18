using System.Text.Json;
using Bookshelf.Shared.Client;
using Bookshelf.Shared.Contracts.Api;
using Microsoft.Extensions.Logging;

namespace Bookshelf.Offline;

public sealed class MauiOfflineSyncService : IOfflineSyncService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly BookshelfApiClient _remoteClient;
    private readonly OfflineStore _store;
    private readonly UserSessionState _sessionState;
    private readonly IConnectivityState _connectivityState;
    private readonly ILogger<MauiOfflineSyncService> _logger;
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private readonly CancellationTokenSource _lifecycleCts = new();
    private PeriodicTimer? _timer;
    private Task? _loopTask;
    private bool _started;

    public MauiOfflineSyncService(
        BookshelfApiClient remoteClient,
        OfflineStore store,
        UserSessionState sessionState,
        IConnectivityState connectivityState,
        ILogger<MauiOfflineSyncService> logger)
    {
        _remoteClient = remoteClient;
        _store = store;
        _sessionState = sessionState;
        _connectivityState = connectivityState;
        _logger = logger;
        StatusText = "Idle";
    }

    public bool IsOffline => !_connectivityState.IsOnline;

    public bool IsSyncing { get; private set; }

    public DateTimeOffset? LastSyncAtUtc { get; private set; }

    public string StatusText { get; private set; }

    public event EventHandler? Changed;

    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        await _store.EnsureInitializedAsync(cancellationToken);
        _connectivityState.Changed += OnConnectivityChanged;
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        _loopTask = RunPeriodicLoopAsync(_lifecycleCts.Token);

        if (_connectivityState.IsOnline)
        {
            _ = TriggerManualSyncAsync(cancellationToken);
        }
        else
        {
            StatusText = "Offline";
            RaiseChanged();
        }
    }

    public async Task TriggerManualSyncAsync(CancellationToken cancellationToken = default)
    {
        await RunSyncAsync("manual", cancellationToken);
    }

    public void Dispose()
    {
        _connectivityState.Changed -= OnConnectivityChanged;
        _lifecycleCts.Cancel();
        _timer?.Dispose();
        try
        {
            _loopTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _syncGate.Dispose();
        _lifecycleCts.Dispose();
    }

    private async Task RunPeriodicLoopAsync(CancellationToken cancellationToken)
    {
        if (_timer is null)
        {
            return;
        }

        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_connectivityState.IsOnline)
                {
                    await RunSyncAsync("timer", cancellationToken);
                }
                else
                {
                    StatusText = "Offline";
                    RaiseChanged();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnConnectivityChanged(object? sender, EventArgs e)
    {
        if (_connectivityState.IsOnline)
        {
            _ = TriggerManualSyncAsync(_lifecycleCts.Token);
        }
        else
        {
            StatusText = "Offline";
            RaiseChanged();
        }
    }

    private async Task RunSyncAsync(string trigger, CancellationToken cancellationToken)
    {
        if (!_connectivityState.IsOnline)
        {
            StatusText = "Offline";
            RaiseChanged();
            return;
        }

        if (!await _syncGate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            IsSyncing = true;
            StatusText = $"Syncing ({trigger})...";
            RaiseChanged();

            await PushQueueAsync(cancellationToken);
            await PullProgressAndHistoryAsync(cancellationToken);
            await PullJobsAndCatalogAsync(cancellationToken);

            LastSyncAtUtc = DateTimeOffset.UtcNow;
            var queueCount = await _store.CountQueueItemsAsync(cancellationToken);
            StatusText = queueCount > 0
                ? $"Synced with pending queue: {queueCount}"
                : "Synced";
        }
        catch (ApiClientException exception) when (IsRetryable(exception))
        {
            StatusText = "Sync retry pending";
            _logger.LogWarning(exception, "Sync operation failed with retryable API exception.");
        }
        catch (Exception exception)
        {
            StatusText = "Sync failed";
            _logger.LogError(exception, "Sync operation failed.");
        }
        finally
        {
            IsSyncing = false;
            RaiseChanged();
            _syncGate.Release();
        }
    }

    private async Task PushQueueAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var batch = await _store.ListReadyQueueItemsAsync(limit: 50, cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            foreach (var item in batch)
            {
                try
                {
                    switch (item.OperationType)
                    {
                        case "upsert_progress":
                        {
                            var request = JsonSerializer.Deserialize<UpsertProgressRequest>(item.PayloadJson, JsonOptions);
                            if (request is null)
                            {
                                await _store.MarkQueueOperationSucceededAsync(item.Id, cancellationToken);
                                continue;
                            }

                            var remoteSnapshot = await _remoteClient.UpsertProgressAsync(request, cancellationToken);
                            await _store.UpsertProgressAsync(remoteSnapshot, cancellationToken);
                            break;
                        }
                        case "append_history":
                        {
                            var request = JsonSerializer.Deserialize<AppendHistoryEventsRequest>(item.PayloadJson, JsonOptions);
                            if (request is null)
                            {
                                await _store.MarkQueueOperationSucceededAsync(item.Id, cancellationToken);
                                continue;
                            }

                            await _remoteClient.AppendHistoryEventsAsync(request, cancellationToken);
                            break;
                        }
                        default:
                            _logger.LogWarning("Unknown queued operation type: {OperationType}", item.OperationType);
                            break;
                    }

                    await _store.MarkQueueOperationSucceededAsync(item.Id, cancellationToken);
                }
                catch (ApiClientException exception) when (IsRetryable(exception))
                {
                    await _store.MarkQueueOperationFailedAsync(
                        item.Id,
                        exception.Message,
                        item.Attempts + 1,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    await _store.MarkQueueOperationFailedAsync(
                        item.Id,
                        exception.Message,
                        item.Attempts + 1,
                        cancellationToken);
                }
            }
        }
    }

    private async Task PullProgressAndHistoryAsync(CancellationToken cancellationToken)
    {
        await PullProgressSnapshotsAsync(cancellationToken);
        await PullHistoryEventsAsync(cancellationToken);
    }

    private async Task PullProgressSnapshotsAsync(CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var page = 1;
        while (true)
        {
            var response = await _remoteClient.ListProgressAsync(
                bookId: null,
                mediaType: null,
                page,
                pageSize,
                cancellationToken);

            foreach (var snapshot in response.Items)
            {
                await _store.UpsertProgressAsync(snapshot, cancellationToken);
            }

            if (response.Items.Count < pageSize || page * pageSize >= response.Total)
            {
                break;
            }

            page++;
        }
    }

    private async Task PullHistoryEventsAsync(CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var page = 1;
        while (true)
        {
            var response = await _remoteClient.ListHistoryEventsAsync(
                bookId: null,
                mediaType: null,
                page,
                pageSize,
                cancellationToken);

            var writes = response.Items
                .Select(item => new HistoryEventWriteDto(
                    BookId: item.BookId,
                    MediaType: item.MediaType,
                    EventType: item.EventType,
                    PositionRef: item.PositionRef,
                    EventAtUtc: item.EventAtUtc))
                .ToArray();
            await _store.AppendHistoryEventsAsync(_sessionState.UserId, writes, cancellationToken);

            if (response.Items.Count < pageSize || page * pageSize >= response.Total)
            {
                break;
            }

            page++;
        }
    }

    private async Task PullJobsAndCatalogAsync(CancellationToken cancellationToken)
    {
        const int pageSize = 20;
        var page = 1;
        while (true)
        {
            var jobs = await _remoteClient.ListDownloadJobsAsync(
                status: null,
                page,
                pageSize,
                cancellationToken);

            foreach (var job in jobs.Items)
            {
                if (!job.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var entry = new LocalMediaEntry(
                    UserId: _sessionState.UserId,
                    BookId: job.BookId,
                    MediaType: job.MediaType,
                    LocalPath: $"downloads/{job.ExternalJobId ?? job.Id.ToString()}",
                    IsAvailable: true,
                    UpdatedAtUtc: job.CompletedAtUtc ?? job.UpdatedAtUtc);
                await _store.UpsertMediaEntryAsync(entry, cancellationToken);
            }

            await _store.SetCacheAsync(
                $"jobs:{_sessionState.UserId}::{page}:{pageSize}",
                jobs,
                TimeSpan.FromMinutes(10),
                cancellationToken);

            if (jobs.Items.Count < pageSize || page * pageSize >= jobs.Total)
            {
                break;
            }

            page++;
        }

        var library = await _remoteClient.GetLibraryAsync(
            includeArchived: false,
            query: null,
            providerCode: null,
            catalogState: null,
            page: 1,
            pageSize: 20,
            cancellationToken: cancellationToken);
        await _store.SetCacheAsync(
            $"library:{_sessionState.UserId}:False::::1:20",
            library,
            TimeSpan.FromDays(7),
            cancellationToken);
    }

    private void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsRetryable(ApiClientException exception)
    {
        return exception.StatusCode >= 500 ||
               exception.StatusCode == 503 ||
               exception.Code.EndsWith("_UNAVAILABLE", StringComparison.OrdinalIgnoreCase) ||
               exception.Code.Equals("NETWORK_REQUIRED", StringComparison.OrdinalIgnoreCase) ||
               exception.Code.Equals("SYNC_FAILED_RETRYABLE", StringComparison.OrdinalIgnoreCase);
    }
}
