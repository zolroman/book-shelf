using Bookshelf.Infrastructure.Models;

namespace Bookshelf.Infrastructure.Services;

public interface ITorrentSearchClient
{
    Task<IReadOnlyList<TorrentCandidate>> SearchAsync(
        string query,
        int maxItems,
        CancellationToken cancellationToken);
}
