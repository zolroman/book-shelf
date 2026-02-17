namespace Bookshelf.Domain.Entities;

public class User
{
    public int Id { get; set; }

    public string Login { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
