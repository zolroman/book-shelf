namespace Bookshelf.Shared.Contracts.Books;

public sealed record BookDetailsDto(
    int Id,
    string Title,
    string OriginalTitle,
    int? PublishYear,
    float? CommunityRating,
    string CoverUrl,
    string Description,
    IReadOnlyList<AuthorDto> Authors,
    IReadOnlyList<BookFormatDto> Formats);
