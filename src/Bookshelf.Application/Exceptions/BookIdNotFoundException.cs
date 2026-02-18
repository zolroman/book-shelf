namespace Bookshelf.Application.Exceptions;

public sealed class BookIdNotFoundException : Exception
{
    public BookIdNotFoundException(long bookId)
        : base($"Book with id '{bookId}' was not found.")
    {
        BookId = bookId;
    }

    public long BookId { get; }
}
