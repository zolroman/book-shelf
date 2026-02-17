namespace Bookshelf.Shared.Contracts.History;

public sealed record AddHistoryEventRequest(
    int UserId,
    int BookId,
    string FormatType,
    string EventType,
    string PositionRef,
    DateTime? EventAtUtc);
