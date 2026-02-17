using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Entities;

public class ProgressSnapshot
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int BookId { get; set; }

    public BookFormatType FormatType { get; set; }

    public string PositionRef { get; private set; } = string.Empty;

    public float ProgressPercent { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(string positionRef, float progressPercent, DateTime updatedAtUtc)
    {
        DomainGuards.RequirePercent(progressPercent, nameof(progressPercent));
        PositionRef = positionRef;
        ProgressPercent = progressPercent;
        UpdatedAtUtc = updatedAtUtc;
    }
}
