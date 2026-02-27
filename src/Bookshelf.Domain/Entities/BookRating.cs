namespace Bookshelf.Domain.Entities;

public sealed class BookRating
{
    private BookRating()
    {
    }

    public BookRating(long userId, long bookId, int rating)
    {
        ValidateRating(rating);
        UserId = userId;
        BookId = bookId;
        Rating = rating;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public long UserId { get; private set; }

    public User? User { get; private set; }

    public long BookId { get; private set; }

    public Book? Book { get; private set; }

    public int Rating { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(int rating, DateTimeOffset? updatedAtUtc = null)
    {
        ValidateRating(rating);
        Rating = rating;
        UpdatedAtUtc = updatedAtUtc ?? DateTimeOffset.UtcNow;
    }

    private static void ValidateRating(int rating)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
        }
    }
}
