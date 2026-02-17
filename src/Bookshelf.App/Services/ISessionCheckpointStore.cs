using Bookshelf.App.Models;

namespace Bookshelf.App.Services;

public interface ISessionCheckpointStore
{
    Task<ReaderSessionCheckpoint?> GetAsync(
        int userId,
        int bookId,
        string formatType,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        ReaderSessionCheckpoint checkpoint,
        CancellationToken cancellationToken = default);
}
