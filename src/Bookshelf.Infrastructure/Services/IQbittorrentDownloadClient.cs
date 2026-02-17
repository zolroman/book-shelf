using Bookshelf.Infrastructure.Models;

namespace Bookshelf.Infrastructure.Services;

public interface IQbittorrentDownloadClient
{
    Task<string> EnqueueAsync(string downloadUri, CancellationToken cancellationToken);

    Task<ExternalDownloadStatus> GetStatusAsync(string externalJobId, CancellationToken cancellationToken);

    Task CancelAsync(string externalJobId, CancellationToken cancellationToken);
}
