using Bookshelf.Shared.Contracts.History;

namespace Bookshelf.App.Models;

public sealed record QueuedHistorySyncPayload(
    AddHistoryEventRequest Request);
