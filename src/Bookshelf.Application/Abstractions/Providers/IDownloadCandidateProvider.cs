namespace Bookshelf.Application.Abstractions.Providers;

public interface IDownloadCandidateProvider
{
    string ProviderCode { get; }

    string ProviderName { get; }

    IReadOnlyCollection<string> SupportedMediaTypes { get; }

    int Priority { get; }

    Task<IReadOnlyList<DownloadCandidateRaw>> SearchAsync(
        DownloadCandidateSearchRequest request,
        int maxItems,
        CancellationToken cancellationToken = default);
}

public sealed record DownloadCandidateSearchRequest(
    string Query,
    string? AuthorFilter = null);

public sealed record DownloadCandidateRaw(
    string Title,
    string DownloadUri,
    string SourceUrl,
    int? Seeders,
    long? SizeBytes,
    DateTimeOffset? PublishedAtUtc,
    string UniqueIdentifier = "",
    string SourceProviderCode = "",
    string SourceProviderName = "",
    string ExecutionProviderCode = "");
