using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Abstractions.Services;

public interface ICandidateDiscoveryService
{
    Task<DownloadCandidatesResponse> FindAsync(
        string providerCode,
        string providerBookKey,
        string mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
