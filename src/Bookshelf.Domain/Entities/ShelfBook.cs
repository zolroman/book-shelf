namespace Bookshelf.Domain.Entities;

public sealed class ShelfBook
{
    private ShelfBook()
    {
    }

    public ShelfBook(long shelfId, long bookId)
    {
        ShelfId = shelfId;
        BookId = bookId;
        AddedAtUtc = DateTimeOffset.UtcNow;
    }

    public long ShelfId { get; private set; }

    public Shelf? Shelf { get; private set; }

    public long BookId { get; private set; }

    public Book? Book { get; private set; }

    public DateTimeOffset AddedAtUtc { get; private set; }
}
