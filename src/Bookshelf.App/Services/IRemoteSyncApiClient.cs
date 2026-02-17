using Bookshelf.Shared.Contracts.History;
using Bookshelf.Shared.Contracts.Progress;

namespace Bookshelf.App.Services;

public interface IRemoteSyncApiClient
{
    Task<ProgressSnapshotDto?> GetProgressRemoteAsync(
        int userId,
        int bookId,
        string formatType,
        CancellationToken cancellationToken = default);

    Task<bool> UpsertProgressRemoteAsync(
        UpsertProgressRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> AddHistoryEventRemoteAsync(
        AddHistoryEventRequest request,
        CancellationToken cancellationToken = default);
}
