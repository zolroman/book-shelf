namespace Bookshelf.Shared.Contracts.Library;

public sealed record AddLibraryItemRequest(
    int UserId,
    int BookId);
