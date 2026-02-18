using Bookshelf.Shared.UI.Models;
using Bookshelf.Shared.UI.Services;

namespace Bookshelf.Web.Services;

public class WebReadingSessionService : IReadingSessionService
{
    public Task<ReaderSessionCheckpoint> LoadAsync(int userId, int bookId, string formatType, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ReaderSessionCheckpoint
        {
            UserId = userId,
            BookId = bookId,
            FormatType = formatType
        });
    }

    public Task SaveCheckpointAsync(ReaderSessionCheckpoint checkpoint, bool syncRemote, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task MarkStartedAsync(ReaderSessionCheckpoint checkpoint, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task MarkCompletedAsync(ReaderSessionCheckpoint checkpoint, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
