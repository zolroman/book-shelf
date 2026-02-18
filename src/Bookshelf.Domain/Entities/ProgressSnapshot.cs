using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Entities;

public sealed class ProgressSnapshot
{
    private ProgressSnapshot()
    {
    }

    public ProgressSnapshot(long userId, long bookId, MediaType mediaType, string positionRef, decimal progressPercent)
    {
        if (string.IsNullOrWhiteSpace(positionRef))
        {
            throw new ArgumentException("Position reference is required.", nameof(positionRef));
        }

        ValidatePercent(progressPercent);

        UserId = userId;
        BookId = bookId;
        MediaType = mediaType;
        PositionRef = positionRef.Trim();
        ProgressPercent = progressPercent;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public long Id { get; private set; }

    public long UserId { get; private set; }

    public User? User { get; private set; }

    public long BookId { get; private set; }

    public Book? Book { get; private set; }

    public MediaType MediaType { get; private set; }

    public string PositionRef { get; private set; } = string.Empty;

    public decimal ProgressPercent { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(string positionRef, decimal progressPercent, DateTimeOffset updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(positionRef))
        {
            throw new ArgumentException("Position reference is required.", nameof(positionRef));
        }

        ValidatePercent(progressPercent);
        PositionRef = positionRef.Trim();
        ProgressPercent = progressPercent;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static void ValidatePercent(decimal progressPercent)
    {
        if (progressPercent < 0m || progressPercent > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(progressPercent), "Progress percent must be between 0 and 100.");
        }
    }
}
