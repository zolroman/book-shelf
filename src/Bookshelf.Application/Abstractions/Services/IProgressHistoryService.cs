using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Abstractions.Services;

public interface IProgressHistoryService
{
    Task<ProgressSnapshotDto> UpsertProgressAsync(
        long userId,
        UpsertProgressRequest request,
        CancellationToken cancellationToken = default);

    Task<ProgressSnapshotsResponse> ListProgressAsync(
        long userId,
        long? bookId,
        string? mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AppendHistoryEventsResponse> AppendHistoryAsync(
        long userId,
        AppendHistoryEventsRequest request,
        CancellationToken cancellationToken = default);

    Task<HistoryEventsResponse> ListHistoryAsync(
        long userId,
        long? bookId,
        string? mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
