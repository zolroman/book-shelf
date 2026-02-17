using Bookshelf.Shared.Contracts.Books;
using Bookshelf.Shared.Contracts.History;
using Bookshelf.Shared.Contracts.Library;

namespace Bookshelf.App.Services;

public interface IBookshelfApiClient
{
    Task<IReadOnlyList<LibraryBookDto>> GetLibraryAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookSummaryDto>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<bool> AddToLibraryAsync(int userId, int bookId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistoryEventDto>> GetHistoryAsync(int userId, CancellationToken cancellationToken = default);
}
