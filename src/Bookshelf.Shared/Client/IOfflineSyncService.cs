namespace Bookshelf.Shared.Client;

public interface IOfflineSyncService
{
    bool IsOffline { get; }

    bool IsSyncing { get; }

    DateTimeOffset? LastSyncAtUtc { get; }

    string StatusText { get; }

    event EventHandler? Changed;

    Task EnsureStartedAsync(CancellationToken cancellationToken = default);

    Task TriggerManualSyncAsync(CancellationToken cancellationToken = default);
}
