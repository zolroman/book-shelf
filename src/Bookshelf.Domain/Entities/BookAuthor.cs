namespace Bookshelf.Domain.Entities;

public sealed class BookAuthor
{
    private BookAuthor()
    {
    }

    public BookAuthor(long bookId, long authorId)
    {
        BookId = bookId;
        AuthorId = authorId;
    }

    public long BookId { get; private set; }

    public Book? Book { get; private set; }

    public long AuthorId { get; private set; }

    public Author? Author { get; private set; }
}
