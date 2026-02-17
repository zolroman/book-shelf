using System.Text.Json;
using Bookshelf.App.Models;
using Bookshelf.Shared.Contracts.History;
using Bookshelf.Shared.Contracts.Progress;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;

namespace Bookshelf.App.Services;

public sealed class OfflineSyncService(
    IOfflineStateStore stateStore,
    IRemoteSyncApiClient remoteSyncApiClient,
    IConnectivity connectivity,
    ILogger<OfflineSyncService> logger) : IOfflineSyncService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(20);

    private readonly IOfflineStateStore _stateStore = stateStore;
    private readonly IRemoteSyncApiClient _remoteSyncApiClient = remoteSyncApiClient;
    private readonly IConnectivity _connectivity = connectivity;
    private readonly ILogger<OfflineSyncService> _logger = logger;
    private readonly SemaphoreSlim _flushMutex = new(1, 1);

    private readonly object _statusSync = new();
    private OfflineSyncStatus _lastStatus = new(false, 0, 0, 0, 0, DateTime.UtcNow);

    private CancellationTokenSource? _backgroundCts;
    private Task? _backgroundTask;
    private int _started;

    public event Action<OfflineSyncStatus>? StatusChanged;

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        _connectivity.ConnectivityChanged += OnConnectivityChanged;

        _backgroundCts = new CancellationTokenSource();
        _backgroundTask = RunBackgroundFlushAsync(_backgroundCts.Token);

        _ = RefreshStatusAsync(CancellationToken.None);
    }

    public async Task<OfflineSyncStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        OfflineSyncStatus previous;
        lock (_statusSync)
        {
            previous = _lastStatus;
        }

        var pending = await _stateStore.GetPendingSyncOperationCountAsync(cancellationToken);
        var status = new OfflineSyncStatus(
            IsOnline(),
            PendingBefore: pending,
            Succeeded: previous.Succeeded,
            Failed: previous.Failed,
            PendingAfter: pending,
            CheckedAtUtc: DateTime.UtcNow);

        UpdateStatus(status, notify: false);
        return status;
    }

    public async Task<OfflineSyncStatus> FlushAsync(CancellationToken cancellationToken = default)
    {
        await _flushMutex.WaitAsync(cancellationToken);
        try
        {
            var pendingBefore = await _stateStore.GetPendingSyncOperationCountAsync(cancellationToken);
            if (!IsOnline())
            {
                var offlineStatus = new OfflineSyncStatus(
                    IsOnline: false,
                    PendingBefore: pendingBefore,
                    Succeeded: 0,
                    Failed: 0,
                    PendingAfter: pendingBefore,
                    CheckedAtUtc: DateTime.UtcNow);

                UpdateStatus(offlineStatus, notify: true);
                return offlineStatus;
            }

            var operations = await _stateStore.GetPendingSyncOperationsAsync(maxItems: 200, cancellationToken);
            var succeeded = 0;
            var failed = 0;

            foreach (var operation in operations)
            {
                var (success, error) = await TrySyncOperationAsync(operation, cancellationToken);
                if (success)
                {
                    await _stateStore.MarkSyncOperationSucceededAsync(operation.Id, cancellationToken);
                    succeeded++;
                    continue;
                }

                await _stateStore.MarkSyncOperationFailedAsync(operation.Id, error ?? "Sync failed.", cancellationToken);
                failed++;
            }

            var pendingAfter = await _stateStore.GetPendingSyncOperationCountAsync(cancellationToken);
            var status = new OfflineSyncStatus(
                IsOnline: true,
                PendingBefore: pendingBefore,
                Succeeded: succeeded,
                Failed: failed,
                PendingAfter: pendingAfter,
                CheckedAtUtc: DateTime.UtcNow);

            UpdateStatus(status, notify: true);
            return status;
        }
        finally
        {
            _flushMutex.Release();
        }
    }

    public async Task<bool> QueueProgressAsync(
        UpsertProgressRequest request,
        DateTime clientUpdatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedFormat = NormalizeFormat(request.FormatType);
        var normalizedRequest = request with { FormatType = normalizedFormat };
        var payload = new QueuedProgressSyncPayload(normalizedRequest, clientUpdatedAtUtc.ToUniversalTime());

        if (IsOnline() && await TrySyncProgressAsync(payload, cancellationToken))
        {
            await RefreshStatusAsync(cancellationToken);
            return true;
        }

        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var dedupKey = $"progress:{normalizedRequest.UserId}:{normalizedRequest.BookId}:{normalizedFormat}";

        await _stateStore.EnqueueSyncOperationAsync(
            SyncOperationType.ProgressUpsert,
            payloadJson,
            dedupKey,
            cancellationToken);

        await RefreshStatusAsync(cancellationToken);
        return true;
    }

    public async Task<bool> QueueHistoryEventAsync(
        AddHistoryEventRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedRequest = request with { FormatType = NormalizeFormat(request.FormatType) };
        var payload = new QueuedHistorySyncPayload(normalizedRequest);

        if (IsOnline() && await TrySyncHistoryEventAsync(payload, cancellationToken))
        {
            await RefreshStatusAsync(cancellationToken);
            return true;
        }

        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        await _stateStore.EnqueueSyncOperationAsync(
            SyncOperationType.HistoryAppend,
            payloadJson,
            dedupKey: null,
            cancellationToken);

        await RefreshStatusAsync(cancellationToken);
        return true;
    }

    public void Dispose()
    {
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;

        try
        {
            _backgroundCts?.Cancel();
            _backgroundTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Offline sync shutdown raised an exception.");
        }
        finally
        {
            _backgroundCts?.Dispose();
            _flushMutex.Dispose();
        }
    }

    private async Task<(bool Success, string? Error)> TrySyncOperationAsync(
        SyncOperationRecord operation,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (operation.OperationType)
            {
                case SyncOperationType.ProgressUpsert:
                {
                    var payload = JsonSerializer.Deserialize<QueuedProgressSyncPayload>(operation.PayloadJson, JsonOptions);
                    if (payload is null)
                    {
                        return (false, "Unable to deserialize progress payload.");
                    }

                    var success = await TrySyncProgressAsync(payload, cancellationToken);
                    return success
                        ? (true, null)
                        : (false, "Progress sync request failed.");
                }
                case SyncOperationType.HistoryAppend:
                {
                    var payload = JsonSerializer.Deserialize<QueuedHistorySyncPayload>(operation.PayloadJson, JsonOptions);
                    if (payload is null)
                    {
                        return (false, "Unable to deserialize history payload.");
                    }

                    var success = await TrySyncHistoryEventAsync(payload, cancellationToken);
                    return success
                        ? (true, null)
                        : (false, "History sync request failed.");
                }
                default:
                    _logger.LogWarning(
                        "Unknown sync operation type '{OperationType}' (id: {OperationId}). Dropping it.",
                        operation.OperationType,
                        operation.Id);
                    return (true, null);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Sync operation {OperationId} ({OperationType}) failed.",
                operation.Id,
                operation.OperationType);

            return (false, exception.Message);
        }
    }

    private async Task<bool> TrySyncProgressAsync(
        QueuedProgressSyncPayload payload,
        CancellationToken cancellationToken)
    {
        var request = payload.Request with { FormatType = NormalizeFormat(payload.Request.FormatType) };

        var remote = await _remoteSyncApiClient.GetProgressRemoteAsync(
            request.UserId,
            request.BookId,
            request.FormatType,
            cancellationToken);

        if (remote is not null && remote.UpdatedAtUtc > payload.ClientUpdatedAtUtc)
        {
            _logger.LogInformation(
                "Skipped local progress update for user {UserId}, book {BookId}, format {FormatType}: remote snapshot is newer ({RemoteUpdatedAtUtc:o} > {ClientUpdatedAtUtc:o}).",
                request.UserId,
                request.BookId,
                request.FormatType,
                remote.UpdatedAtUtc,
                payload.ClientUpdatedAtUtc);

            return true;
        }

        return await _remoteSyncApiClient.UpsertProgressRemoteAsync(request, cancellationToken);
    }

    private Task<bool> TrySyncHistoryEventAsync(
        QueuedHistorySyncPayload payload,
        CancellationToken cancellationToken)
    {
        return _remoteSyncApiClient.AddHistoryEventRemoteAsync(payload.Request, cancellationToken);
    }

    private async Task RunBackgroundFlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(FlushInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // expected on app shutdown
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Background offline sync loop terminated unexpectedly.");
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs args)
    {
        _ = args.NetworkAccess switch
        {
            NetworkAccess.Internet or NetworkAccess.ConstrainedInternet => FlushAsync(CancellationToken.None),
            _ => RefreshStatusAsync(CancellationToken.None)
        };
    }

    private async Task RefreshStatusAsync(CancellationToken cancellationToken)
    {
        var pending = await _stateStore.GetPendingSyncOperationCountAsync(cancellationToken);
        var status = new OfflineSyncStatus(
            IsOnline(),
            PendingBefore: pending,
            Succeeded: 0,
            Failed: 0,
            PendingAfter: pending,
            CheckedAtUtc: DateTime.UtcNow);

        UpdateStatus(status, notify: true);
    }

    private void UpdateStatus(OfflineSyncStatus status, bool notify)
    {
        lock (_statusSync)
        {
            _lastStatus = status;
        }

        if (!notify)
        {
            return;
        }

        try
        {
            StatusChanged?.Invoke(status);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Offline sync status listener raised an exception.");
        }
    }

    private bool IsOnline()
    {
        return IsOnline(_connectivity.NetworkAccess);
    }

    private static bool IsOnline(NetworkAccess networkAccess)
    {
        return networkAccess is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet;
    }

    private static string NormalizeFormat(string formatType)
    {
        return string.Equals(formatType, "audio", StringComparison.OrdinalIgnoreCase) ? "audio" : "text";
    }
}
