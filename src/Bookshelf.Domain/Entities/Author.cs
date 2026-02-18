namespace Bookshelf.Domain.Entities;

public sealed class Author
{
    private Author()
    {
    }

    public Author(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Author name is required.", nameof(name));
        }

        Name = name.Trim();
    }

    public long Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public ICollection<BookAuthor> BookAuthors { get; } = new List<BookAuthor>();
}
