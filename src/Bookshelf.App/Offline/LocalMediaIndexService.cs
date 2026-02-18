using Bookshelf.Shared.Client;

namespace Bookshelf.Offline;

public sealed class LocalMediaIndexService : ILocalMediaIndexService
{
    private readonly OfflineStore _store;
    private readonly UserSessionState _sessionState;

    public LocalMediaIndexService(OfflineStore store, UserSessionState sessionState)
    {
        _store = store;
        _sessionState = sessionState;
    }

    public Task<LocalMediaEntry?> GetAsync(
        long bookId,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        return _store.GetMediaEntryAsync(_sessionState.UserId, bookId, mediaType, cancellationToken);
    }
}
