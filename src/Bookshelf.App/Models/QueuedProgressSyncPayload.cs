using Bookshelf.Shared.Contracts.Progress;

namespace Bookshelf.App.Models;

public sealed record QueuedProgressSyncPayload(
    UpsertProgressRequest Request,
    DateTime ClientUpdatedAtUtc);
