namespace Bookshelf.Infrastructure.Models;

public sealed record ImportedBookSeed(
    string Title,
    string OriginalTitle,
    int? PublishYear,
    float? CommunityRating,
    string CoverUrl,
    string Description,
    IReadOnlyList<string> Authors,
    bool HasText,
    bool HasAudio);
