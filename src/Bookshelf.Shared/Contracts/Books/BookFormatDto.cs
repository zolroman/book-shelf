namespace Bookshelf.Shared.Contracts.Books;

public sealed record BookFormatDto(
    int Id,
    string FormatType,
    string Language,
    int? DurationSeconds,
    long FileSizeBytes);
