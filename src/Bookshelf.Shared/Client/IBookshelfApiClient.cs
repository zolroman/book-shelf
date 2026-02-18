using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Shared.Client;

public interface IBookshelfApiClient
{
    Task<SearchBooksResponse> SearchBooksAsync(
        string? title,
        string? author,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<SearchBookDetailsResponse> GetBookDetailsAsync(
        string providerCode,
        string providerBookKey,
        CancellationToken cancellationToken = default);

    Task<DownloadCandidatesResponse> GetCandidatesAsync(
        string providerCode,
        string providerBookKey,
        string mediaType,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<AddAndDownloadResponse> AddAndDownloadAsync(
        string providerCode,
        string providerBookKey,
        string mediaType,
        string candidateId,
        CancellationToken cancellationToken = default);

    Task<DownloadJobsResponse> ListDownloadJobsAsync(
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<DownloadJobDto> GetDownloadJobAsync(
        long jobId,
        CancellationToken cancellationToken = default);

    Task<DownloadJobDto> CancelDownloadJobAsync(
        long jobId,
        CancellationToken cancellationToken = default);

    Task<LibraryResponse> GetLibraryAsync(
        bool includeArchived,
        string? query,
        string? providerCode,
        string? catalogState,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<ShelvesResponse> GetShelvesAsync(CancellationToken cancellationToken = default);

    Task<CreateShelfResponse> CreateShelfAsync(string name, CancellationToken cancellationToken = default);

    Task<AddBookToShelfResponse> AddBookToShelfAsync(
        long shelfId,
        long bookId,
        CancellationToken cancellationToken = default);

    Task RemoveBookFromShelfAsync(
        long shelfId,
        long bookId,
        CancellationToken cancellationToken = default);

    Task<ProgressSnapshotDto> UpsertProgressAsync(
        UpsertProgressRequest request,
        CancellationToken cancellationToken = default);

    Task<ProgressSnapshotsResponse> ListProgressAsync(
        long? bookId,
        string? mediaType,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<AppendHistoryEventsResponse> AppendHistoryEventsAsync(
        AppendHistoryEventsRequest request,
        CancellationToken cancellationToken = default);

    Task<HistoryEventsResponse> ListHistoryEventsAsync(
        long? bookId,
        string? mediaType,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
