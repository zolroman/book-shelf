using Bookshelf.App.Models;

namespace Bookshelf.App.Services;

public interface IReadingSessionService
{
    Task<ReaderSessionCheckpoint> LoadAsync(
        int userId,
        int bookId,
        string formatType,
        CancellationToken cancellationToken = default);

    Task SaveCheckpointAsync(
        ReaderSessionCheckpoint checkpoint,
        bool syncRemote,
        CancellationToken cancellationToken = default);

    Task MarkStartedAsync(
        ReaderSessionCheckpoint checkpoint,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        ReaderSessionCheckpoint checkpoint,
        CancellationToken cancellationToken = default);
}
