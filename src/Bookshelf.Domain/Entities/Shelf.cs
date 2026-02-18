namespace Bookshelf.Domain.Entities;

public sealed class Shelf
{
    private Shelf()
    {
    }

    public Shelf(long userId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Shelf name is required.", nameof(name));
        }

        UserId = userId;
        Name = name.Trim();
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public long Id { get; private set; }

    public long UserId { get; private set; }

    public User? User { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public ICollection<ShelfBook> ShelfBooks { get; } = new List<ShelfBook>();

    public bool ContainsBook(long bookId)
    {
        return ShelfBooks.Any(x => x.BookId == bookId);
    }

    public void AddBook(long bookId)
    {
        if (ContainsBook(bookId))
        {
            return;
        }

        ShelfBooks.Add(new ShelfBook(Id, bookId));
    }
}
