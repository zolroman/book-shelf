namespace Bookshelf.Application.Abstractions.Providers;

public interface IDownloadCandidateProvider
{
    string ProviderCode { get; }

    Task<IReadOnlyList<DownloadCandidateRaw>> SearchAsync(
        string query,
        int maxItems,
        CancellationToken cancellationToken = default);
}

public sealed record DownloadCandidateRaw(
    string Title,
    string DownloadUri,
    string SourceUrl,
    int? Seeders,
    long? SizeBytes,
    DateTimeOffset? PublishedAtUtc);
