namespace Bookshelf.Shared.Contracts.Books;

public sealed record BookSummaryDto(
    int Id,
    string Title,
    string OriginalTitle,
    int? PublishYear,
    float? CommunityRating,
    string CoverUrl,
    IReadOnlyList<AuthorDto> Authors,
    bool HasText,
    bool HasAudio);
