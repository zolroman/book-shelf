namespace Bookshelf.Shared.Contracts.Library;

public sealed record LibraryItemDto(
    int Id,
    int UserId,
    int BookId,
    string Status,
    float? UserRating,
    DateTime AddedAtUtc);
