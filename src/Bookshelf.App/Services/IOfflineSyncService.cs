using Bookshelf.App.Models;
using Bookshelf.Shared.Contracts.History;
using Bookshelf.Shared.Contracts.Progress;

namespace Bookshelf.App.Services;

public interface IOfflineSyncService
{
    event Action<OfflineSyncStatus>? StatusChanged;

    void Start();

    Task<OfflineSyncStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<OfflineSyncStatus> FlushAsync(CancellationToken cancellationToken = default);

    Task<bool> QueueProgressAsync(
        UpsertProgressRequest request,
        DateTime clientUpdatedAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> QueueHistoryEventAsync(
        AddHistoryEventRequest request,
        CancellationToken cancellationToken = default);
}
