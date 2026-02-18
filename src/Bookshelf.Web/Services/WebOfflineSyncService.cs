using Bookshelf.Shared.Contracts.History;
using Bookshelf.Shared.Contracts.Progress;
using Bookshelf.Shared.Contracts.Sync;

namespace Bookshelf.Web.Services;

public class WebOfflineSyncService : IOfflineSyncService
{
    public event Action<OfflineSyncStatus>? StatusChanged;

    public void Start() { }

    public Task<OfflineSyncStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new OfflineSyncStatus(
            IsOnline: true,
            PendingBefore: 0,
            Succeeded: 0,
            Failed: 0,
            PendingAfter: 0,
            CheckedAtUtc: DateTime.UtcNow));
    }

    public Task<OfflineSyncStatus> FlushAsync(CancellationToken cancellationToken = default) => GetStatusAsync(cancellationToken);

    public Task<bool> QueueProgressAsync(UpsertProgressRequest request, DateTime clientUpdatedAtUtc, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<bool> QueueHistoryEventAsync(AddHistoryEventRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
