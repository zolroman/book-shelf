using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Services;

public sealed class CandidateDiscoveryService : ICandidateDiscoveryService
{
    private static readonly string[] AudioKeywords = ["audiobook", "audio", "mp3", "m4b"];
    private static readonly string[] TextKeywords = ["epub", "pdf", "fb2", "mobi", "txt"];
    private readonly IReadOnlyDictionary<string, IDownloadCandidateProvider> _providerByCode;
    private readonly IBookSearchService _bookSearchService;

    public CandidateDiscoveryService(
        IEnumerable<IDownloadCandidateProvider> providers,
        IBookSearchService bookSearchService)
    {
        _providerByCode = providers.ToDictionary(
            keySelector: x => x.ProviderCode,
            elementSelector: x => x,
            comparer: StringComparer.OrdinalIgnoreCase);
        _bookSearchService = bookSearchService;
    }

    public async Task<DownloadCandidatesResponse> FindAsync(
        string providerCode,
        string providerBookKey,
        string mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var normalizedMediaType = mediaType.Trim().ToLowerInvariant();
        var provider = GetProvider("jackett");

        var details = await _bookSearchService.GetDetailsAsync(providerCode, providerBookKey, cancellationToken);
        if (details is null)
        {
            return new DownloadCandidatesResponse(
                ProviderCode: providerCode,
                ProviderBookKey: providerBookKey,
                MediaType: normalizedMediaType,
                Page: safePage,
                PageSize: safePageSize,
                Total: 0,
                Items: Array.Empty<DownloadCandidateDto>());
        }

        var queries = BuildQueries(details);
        var allCandidates = new List<(DownloadCandidateRaw Candidate, string ClassifiedType)>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in queries)
        {
            var candidates = await provider.SearchAsync(query, maxItems: int.MaxValue, cancellationToken);
            foreach (var candidate in candidates)
            {
                var dedupKey = BuildCandidateId(candidate);
                if (!seenKeys.Add(dedupKey))
                {
                    continue;
                }

                var classified = ClassifyMediaType(candidate.Title);
                if (classified != normalizedMediaType && classified != "unknown")
                {
                    continue;
                }

                allCandidates.Add((candidate, classified));
            }
        }

        var ranked = allCandidates
            .Select(x => new RankedCandidate(
                x.Candidate,
                x.ClassifiedType,
                ComputeTitleMatchScore(details, x.Candidate),
                x.Candidate.Seeders ?? 0,
                ComputeSizeSanityScore(normalizedMediaType, x.Candidate),
                x.Candidate.PublishedAtUtc ?? DateTimeOffset.MinValue))
            .OrderByDescending(x => x.TitleMatchScore)
            .ThenByDescending(x => x.Seeders)
            .ThenByDescending(x => x.SizeSanityScore)
            .ThenByDescending(x => x.PublishedAtUtc)
            .ThenBy(x => x.Candidate.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var paged = ranked
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new DownloadCandidateDto(
                CandidateId: BuildCandidateId(x.Candidate),
                MediaType: x.ClassifiedType,
                Title: x.Candidate.Title,
                DownloadUri: x.Candidate.DownloadUri,
                SourceUrl: x.Candidate.SourceUrl,
                Seeders: x.Candidate.Seeders,
                SizeBytes: x.Candidate.SizeBytes))
            .ToArray();

        return new DownloadCandidatesResponse(
            ProviderCode: providerCode,
            ProviderBookKey: providerBookKey,
            MediaType: normalizedMediaType,
            Page: safePage,
            PageSize: safePageSize,
            Total: ranked.Count,
            Items: paged);
    }

    public async Task<DownloadCandidateDto?> ResolveAsync(
        string providerCode,
        string providerBookKey,
        string mediaType,
        string candidateId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(candidateId))
        {
            return null;
        }

        var response = await FindAsync(
            providerCode,
            providerBookKey,
            mediaType,
            page: 1,
            pageSize: 100,
            cancellationToken);

        return response.Items.FirstOrDefault(
            x => x.CandidateId.Equals(candidateId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private IDownloadCandidateProvider GetProvider(string providerCode)
    {
        if (_providerByCode.TryGetValue(providerCode, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"Download candidate provider '{providerCode}' is not registered.");
    }

    private static IReadOnlyList<string> BuildQueries(SearchBookDetailsResponse details)
    {
        var author = details.Authors.FirstOrDefault();
        var queries = new List<string>();

        if (!string.IsNullOrWhiteSpace(details.Title) && !string.IsNullOrWhiteSpace(author))
        {
            queries.Add($"{details.Title} {author}");
        }

        if (!string.IsNullOrWhiteSpace(details.Title))
        {
            queries.Add(details.Title);
        }

        if (!string.IsNullOrWhiteSpace(details.OriginalTitle) && !string.IsNullOrWhiteSpace(author))
        {
            queries.Add($"{details.OriginalTitle} {author}");
        }

        return queries
            .Select(NormalizeQueryTerm)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    private static int ComputeTitleMatchScore(
        SearchBookDetailsResponse details,
        DownloadCandidateRaw candidate)
    {
        var titleNormalized = candidate.Title.ToLowerInvariant();
        var expectedTitle = details.Title?.ToLowerInvariant() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(expectedTitle) &&
            titleNormalized.Equals(expectedTitle, StringComparison.Ordinal))
        {
            return 2;
        }

        if (!string.IsNullOrWhiteSpace(expectedTitle) &&
            titleNormalized.Contains(expectedTitle, StringComparison.Ordinal))
        {
            return 1;
        }

        var expectedOriginalTitle = details.OriginalTitle?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(expectedOriginalTitle) &&
            titleNormalized.Contains(expectedOriginalTitle, StringComparison.Ordinal))
        {
            return 1;
        }

        return 0;
    }

    private static int ComputeSizeSanityScore(
        string requestedMediaType,
        DownloadCandidateRaw candidate)
    {
        if (!candidate.SizeBytes.HasValue || candidate.SizeBytes.Value <= 0)
        {
            return 0;
        }

        if (requestedMediaType == "audio" && candidate.SizeBytes.Value >= 100L * 1024 * 1024)
        {
            return 1;
        }

        if (requestedMediaType == "text" && candidate.SizeBytes.Value <= 100L * 1024 * 1024)
        {
            return 1;
        }

        return 0;
    }

    private static string ClassifyMediaType(string title)
    {
        var normalized = title.ToLowerInvariant();

        if (AudioKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)))
        {
            return "audio";
        }

        if (TextKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)))
        {
            return "text";
        }

        return "unknown";
    }

    private static string BuildCandidateId(DownloadCandidateRaw candidate)
    {
        var uniqueIdentifier = candidate.UniqueIdentifier?.Trim();
        if (!string.IsNullOrWhiteSpace(uniqueIdentifier))
        {
            return uniqueIdentifier;
        }

        return $"{candidate.DownloadUri}|{candidate.SourceUrl}";
    }

    private static string NormalizeQueryTerm(string value)
    {
        return string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record RankedCandidate(
        DownloadCandidateRaw Candidate,
        string ClassifiedType,
        int TitleMatchScore,
        int Seeders,
        int SizeSanityScore,
        DateTimeOffset PublishedAtUtc);
}
