using Bookshelf.Shared.Contracts.Books;
using Bookshelf.Shared.Contracts.History;
using Bookshelf.Shared.Contracts.Library;
using Bookshelf.Shared.Contracts.Progress;

namespace Bookshelf.App.Services;

public interface IBookshelfApiClient
{
    Task<BookDetailsDto?> GetBookDetailsAsync(int bookId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryBookDto>> GetLibraryAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookSummaryDto>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<bool> AddToLibraryAsync(int userId, int bookId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistoryEventDto>> GetHistoryAsync(int userId, CancellationToken cancellationToken = default);

    Task<ProgressSnapshotDto?> GetProgressAsync(
        int userId,
        int bookId,
        string formatType,
        CancellationToken cancellationToken = default);

    Task<ProgressSnapshotDto?> UpsertProgressAsync(
        UpsertProgressRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> AddHistoryEventAsync(
        AddHistoryEventRequest request,
        CancellationToken cancellationToken = default);
}
