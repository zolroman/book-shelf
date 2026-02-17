using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Bookshelf.Infrastructure.Models;

namespace Bookshelf.Infrastructure.Services;

public interface IBookshelfRepository
{
    Task<User?> GetUserAsync(int userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Book>> GetBooksAsync(string? query, string? author, CancellationToken cancellationToken);

    Task<Book?> GetBookAsync(int bookId, CancellationToken cancellationToken);

    Task<Book> UpsertImportedBookAsync(ImportedBookSeed seed, CancellationToken cancellationToken);

    Task<IReadOnlyList<Author>> GetAuthorsForBookAsync(int bookId, CancellationToken cancellationToken);

    Task<IReadOnlyList<BookFormat>> GetFormatsForBookAsync(int bookId, CancellationToken cancellationToken);

    Task<BookFormat?> GetBookFormatAsync(int bookFormatId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LibraryItem>> GetLibraryItemsAsync(int userId, CancellationToken cancellationToken);

    Task<LibraryItem?> GetLibraryItemAsync(int userId, int bookId, CancellationToken cancellationToken);

    Task<LibraryItem> AddLibraryItemAsync(int userId, int bookId, CancellationToken cancellationToken);

    Task<bool> RemoveLibraryItemAsync(int userId, int bookId, CancellationToken cancellationToken);

    Task<ProgressSnapshot?> GetProgressSnapshotAsync(
        int userId,
        int bookId,
        BookFormatType formatType,
        CancellationToken cancellationToken);

    Task<ProgressSnapshot> UpsertProgressSnapshotAsync(
        int userId,
        int bookId,
        BookFormatType formatType,
        string positionRef,
        float progressPercent,
        CancellationToken cancellationToken);

    Task<HistoryEvent> AddHistoryEventAsync(
        int userId,
        int bookId,
        BookFormatType formatType,
        HistoryEventType eventType,
        string positionRef,
        DateTime eventAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HistoryEvent>> GetHistoryEventsAsync(
        int userId,
        int? bookId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DownloadJob>> GetDownloadJobsAsync(int userId, CancellationToken cancellationToken);

    Task<DownloadJob?> GetDownloadJobAsync(int jobId, CancellationToken cancellationToken);

    Task<DownloadJob?> GetActiveDownloadJobAsync(int userId, int bookFormatId, CancellationToken cancellationToken);

    Task<DownloadJob> CreateDownloadJobAsync(
        int userId,
        int bookFormatId,
        string source,
        CancellationToken cancellationToken);

    Task<DownloadJob> UpdateDownloadJobExternalIdAsync(
        int jobId,
        string externalJobId,
        CancellationToken cancellationToken);

    Task<DownloadJob> UpdateDownloadJobStatusAsync(
        int jobId,
        DownloadJobStatus status,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LocalAsset>> GetLocalAssetsAsync(int userId, CancellationToken cancellationToken);

    Task<LocalAsset> AddOrUpdateLocalAssetAsync(
        int userId,
        int bookFormatId,
        string localPath,
        long fileSizeBytes,
        CancellationToken cancellationToken);

    Task<bool> MarkLocalAssetDeletedAsync(int userId, int bookFormatId, CancellationToken cancellationToken);
}
