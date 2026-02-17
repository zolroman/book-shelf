namespace Bookshelf.Shared.Contracts.History;

public sealed record HistoryEventDto(
    int Id,
    int UserId,
    int BookId,
    string FormatType,
    string EventType,
    string PositionRef,
    DateTime EventAtUtc);
