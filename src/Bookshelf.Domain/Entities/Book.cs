namespace Bookshelf.Domain.Entities;

public class Book
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public int? PublishYear { get; set; }

    public string Description { get; set; } = string.Empty;

    public string CoverUrl { get; set; } = string.Empty;

    public float? CommunityRating { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    
    public virtual ICollection<Author> Authors { get; set; } = new List<Author>();
}
