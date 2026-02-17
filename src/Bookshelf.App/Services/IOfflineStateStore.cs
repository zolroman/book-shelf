using Bookshelf.App.Models;

namespace Bookshelf.App.Services;

public interface IOfflineStateStore
{
    Task SaveMetadataAsync(string key, string payloadJson, CancellationToken cancellationToken = default);

    Task<string?> LoadMetadataAsync(string key, CancellationToken cancellationToken = default);

    Task EnqueueSyncOperationAsync(
        string operationType,
        string payloadJson,
        string? dedupKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SyncOperationRecord>> GetPendingSyncOperationsAsync(
        int maxItems,
        CancellationToken cancellationToken = default);

    Task<int> GetPendingSyncOperationCountAsync(CancellationToken cancellationToken = default);

    Task MarkSyncOperationSucceededAsync(long operationId, CancellationToken cancellationToken = default);

    Task MarkSyncOperationFailedAsync(
        long operationId,
        string error,
        CancellationToken cancellationToken = default);

    Task UpsertLocalAssetAsync(LocalAssetIndexRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LocalAssetIndexRecord>> GetLocalAssetsAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task MarkLocalAssetDeletedAsync(
        int userId,
        int bookFormatId,
        DateTime deletedAtUtc,
        CancellationToken cancellationToken = default);
}
