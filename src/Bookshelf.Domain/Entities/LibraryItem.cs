using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Entities;

public class LibraryItem
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int BookId { get; set; }

    public LibraryItemStatus Status { get; private set; } = LibraryItemStatus.OnShelf;

    public float? UserRating { get; private set; }

    public DateTime AddedAtUtc { get; set; }

    public void SetStatus(LibraryItemStatus status)
    {
        Status = status;
    }

    public void SetRating(float rating)
    {
        DomainGuards.RequireRating(rating, nameof(rating));
        UserRating = rating;
    }
}
