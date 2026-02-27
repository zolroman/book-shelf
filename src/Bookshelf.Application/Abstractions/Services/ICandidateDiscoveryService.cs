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

    Task<ResolvedDownloadCandidate?> ResolveAsync(
        string providerCode,
        string providerBookKey,
        string mediaType,
        string candidateId,
        CancellationToken cancellationToken = default);
}

public sealed record ResolvedDownloadCandidate(
    string CandidateId,
    string MediaType,
    string Title,
    string DownloadUri,
    string SourceUrl,
    string SourceProviderCode,
    string SourceProviderName,
    string ExecutionProviderCode,
    int? Seeders,
    long? SizeBytes);
