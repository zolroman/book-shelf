namespace Bookshelf.App.Models;

public sealed record OfflineSyncStatus(
    bool IsOnline,
    int PendingBefore,
    int Succeeded,
    int Failed,
    int PendingAfter,
    DateTime CheckedAtUtc);
