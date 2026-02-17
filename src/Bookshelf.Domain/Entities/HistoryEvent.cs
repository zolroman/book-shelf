using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Entities;

public class HistoryEvent
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int BookId { get; set; }

    public BookFormatType FormatType { get; set; }

    public HistoryEventType EventType { get; set; }

    public string PositionRef { get; set; } = string.Empty;

    public DateTime EventAtUtc { get; set; }
}
