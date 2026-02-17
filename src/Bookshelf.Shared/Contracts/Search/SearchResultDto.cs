using Bookshelf.Shared.Contracts.Books;

namespace Bookshelf.Shared.Contracts.Search;

public sealed record SearchResultDto(
    string Query,
    IReadOnlyList<BookSummaryDto> Items);
