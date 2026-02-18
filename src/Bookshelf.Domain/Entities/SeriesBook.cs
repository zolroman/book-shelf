namespace Bookshelf.Domain.Entities;

public sealed class SeriesBook
{
    private SeriesBook()
    {
    }

    public SeriesBook(long seriesId, long bookId, int seriesOrder)
    {
        if (seriesOrder <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seriesOrder), "Series order must be greater than zero.");
        }

        SeriesId = seriesId;
        BookId = bookId;
        SeriesOrder = seriesOrder;
    }

    public long SeriesId { get; private set; }

    public Series? Series { get; private set; }

    public long BookId { get; private set; }

    public Book? Book { get; private set; }

    public int SeriesOrder { get; private set; }
}
