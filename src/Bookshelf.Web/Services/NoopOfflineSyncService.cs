using Bookshelf.Shared.Client;

namespace Bookshelf.Web.Services;

public sealed class NoopOfflineSyncService : IOfflineSyncService
{
    public bool IsOffline => false;

    public bool IsSyncing => false;

    public DateTimeOffset? LastSyncAtUtc { get; private set; }

    public string StatusText => "Online";

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        LastSyncAtUtc = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task TriggerManualSyncAsync(CancellationToken cancellationToken = default)
    {
        LastSyncAtUtc = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}
