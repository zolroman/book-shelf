using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Entities;

public sealed class HistoryEvent
{
    private HistoryEvent()
    {
    }

    public HistoryEvent(
        long userId,
        long bookId,
        MediaType mediaType,
        HistoryEventType eventType,
        string? positionRef,
        DateTimeOffset eventAtUtc)
    {
        UserId = userId;
        BookId = bookId;
        MediaType = mediaType;
        EventType = eventType;
        PositionRef = string.IsNullOrWhiteSpace(positionRef) ? null : positionRef.Trim();
        EventAtUtc = eventAtUtc;
    }

    public long Id { get; private set; }

    public long UserId { get; private set; }

    public User? User { get; private set; }

    public long BookId { get; private set; }

    public Book? Book { get; private set; }

    public MediaType MediaType { get; private set; }

    public HistoryEventType EventType { get; private set; }

    public string? PositionRef { get; private set; }

    public DateTimeOffset EventAtUtc { get; private set; }
}
