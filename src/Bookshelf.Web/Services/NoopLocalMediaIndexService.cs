using Bookshelf.Shared.Client;

namespace Bookshelf.Web.Services;

public sealed class NoopLocalMediaIndexService : ILocalMediaIndexService
{
    public Task<LocalMediaEntry?> GetAsync(
        long bookId,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<LocalMediaEntry?>(null);
    }
}
