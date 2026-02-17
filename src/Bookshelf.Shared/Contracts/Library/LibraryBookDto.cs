using Bookshelf.Shared.Contracts.Books;

namespace Bookshelf.Shared.Contracts.Library;

public sealed record LibraryBookDto(
    LibraryItemDto LibraryItem,
    BookSummaryDto Book);
