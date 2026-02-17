using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Entities;

public class BookFormat
{
    public int Id { get; set; }

    public int BookId { get; set; }

    public BookFormatType FormatType { get; set; }

    public string Language { get; set; } = "en";

    public int? DurationSeconds { get; set; }

    public long FileSizeBytes { get; set; }

    public string Checksum { get; set; } = string.Empty;
}
