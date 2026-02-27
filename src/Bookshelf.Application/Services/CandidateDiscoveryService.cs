using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Services;

public sealed class CandidateDiscoveryService : ICandidateDiscoveryService
{
    private const string CandidateProviderUnavailableCode = "CANDIDATE_PROVIDER_UNAVAILABLE";
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
                Items: Array.Empty<DownloadCandidateDto>(),
                Groups: Array.Empty<DownloadCandidateGroupDto>());
        }

        var providers = GetProviders(normalizedMediaType);
        var requests = BuildSearchRequests(details);
        var groupTasks = providers
            .Select(provider => BuildGroupAsync(provider, normalizedMediaType, requests, details, cancellationToken))
            .ToArray();
        var groups = await Task.WhenAll(groupTasks);

        if (groups.All(x => x.Error is not null))
        {
            var topLevelProviderCode = groups.Length == 1
                ? groups[0].Provider.ProviderCode
                : CandidateProviderUnavailableCode;
            throw new DownloadCandidateProviderUnavailableException(
                topLevelProviderCode,
                "All candidate providers are unavailable.",
                groups.First(x => x.Error is not null).Error);
        }

        var pagedGroups = ApplyPaging(groups, safePage, safePageSize)
            .Select(x => new DownloadCandidateGroupDto(
                SourceProviderCode: x.Provider.ProviderCode,
                SourceProviderName: x.Provider.ProviderName,
                Total: x.Total,
                ErrorCode: x.ErrorCode,
                ErrorMessage: x.ErrorMessage,
                Items: x.Items))
            .ToArray();

        var items = pagedGroups
            .SelectMany(x => x.Items)
            .ToArray();

        return new DownloadCandidatesResponse(
            ProviderCode: providerCode,
            ProviderBookKey: providerBookKey,
            MediaType: normalizedMediaType,
            Page: safePage,
            PageSize: safePageSize,
            Total: groups.Sum(x => x.Total),
            Items: items,
            Groups: pagedGroups);
    }

    public async Task<ResolvedDownloadCandidate?> ResolveAsync(
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

        foreach (var group in response.Groups ?? Array.Empty<DownloadCandidateGroupDto>())
        {
            var match = group.Items.FirstOrDefault(
                x => x.CandidateId.Equals(candidateId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                continue;
            }

            return new ResolvedDownloadCandidate(
                CandidateId: match.CandidateId,
                MediaType: match.MediaType,
                Title: match.Title,
                DownloadUri: match.DownloadUri,
                SourceUrl: match.SourceUrl,
                SourceProviderCode: match.SourceProviderCode,
                SourceProviderName: match.SourceProviderName,
                ExecutionProviderCode: GetExecutionProviderCode(match.CandidateId),
                Seeders: match.Seeders,
                SizeBytes: match.SizeBytes);
        }

        return null;
    }

    private IReadOnlyList<IDownloadCandidateProvider> GetProviders(string mediaType)
    {
        return _providerByCode.Values
            .Where(x => x.SupportedMediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<ProviderGroupResult> BuildGroupAsync(
        IDownloadCandidateProvider provider,
        string requestedMediaType,
        IReadOnlyDictionary<string, IReadOnlyList<DownloadCandidateSearchRequest>> requestsByProvider,
        SearchBookDetailsResponse details,
        CancellationToken cancellationToken)
    {
        try
        {
            var allCandidates = new List<(DownloadCandidateRaw Candidate, string ClassifiedType)>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var requests = GetSearchRequests(provider, requestsByProvider);

            foreach (var request in requests)
            {
                var candidates = await provider.SearchAsync(request, maxItems: int.MaxValue, cancellationToken);
                foreach (var candidate in candidates)
                {
                    var candidateId = BuildCandidateId(candidate, provider.ProviderCode);
                    if (!seenKeys.Add(candidateId))
                    {
                        continue;
                    }

                    var classified = ClassifyMediaType(candidate.Title);
                    if (classified != requestedMediaType && classified != "unknown")
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
                    ComputeSizeSanityScore(requestedMediaType, x.Candidate),
                    x.Candidate.PublishedAtUtc ?? DateTimeOffset.MinValue))
                .OrderByDescending(x => x.TitleMatchScore)
                .ThenByDescending(x => x.Seeders)
                .ThenByDescending(x => x.SizeSanityScore)
                .ThenByDescending(x => x.PublishedAtUtc)
                .ThenBy(x => x.Candidate.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var items = ranked
                .Select(x => MapCandidate(provider, x.Candidate, x.ClassifiedType))
                .ToArray();

            return new ProviderGroupResult(provider, ranked.Count, items, null);
        }
        catch (DownloadCandidateProviderUnavailableException exception)
        {
            return new ProviderGroupResult(provider, 0, Array.Empty<DownloadCandidateDto>(), exception);
        }
    }

    private static IReadOnlyList<PagedProviderGroupResult> ApplyPaging(
        IReadOnlyList<ProviderGroupResult> groups,
        int page,
        int pageSize)
    {
        var skip = Math.Max(0, (page - 1) * pageSize);
        var remainingSkip = skip;
        var remainingTake = pageSize;
        var pagedGroups = new List<PagedProviderGroupResult>(groups.Count);

        foreach (var group in groups)
        {
            if (group.Error is not null)
            {
                pagedGroups.Add(new PagedProviderGroupResult(
                    group.Provider,
                    group.Total,
                    GetErrorCode(group.Provider.ProviderCode),
                    group.Error.Message,
                    Array.Empty<DownloadCandidateDto>()));
                continue;
            }

            if (remainingTake <= 0)
            {
                pagedGroups.Add(new PagedProviderGroupResult(
                    group.Provider,
                    group.Total,
                    null,
                    null,
                    Array.Empty<DownloadCandidateDto>()));
                continue;
            }

            var groupSkip = Math.Min(remainingSkip, group.Items.Count);
            remainingSkip -= groupSkip;

            var slice = group.Items
                .Skip(groupSkip)
                .Take(remainingTake)
                .ToArray();
            remainingTake -= slice.Length;

            pagedGroups.Add(new PagedProviderGroupResult(
                group.Provider,
                group.Total,
                null,
                null,
                slice));
        }

        return pagedGroups;
    }

    private static DownloadCandidateDto MapCandidate(
        IDownloadCandidateProvider provider,
        DownloadCandidateRaw candidate,
        string mediaType)
    {
        return new DownloadCandidateDto(
            CandidateId: BuildCandidateId(candidate, provider.ProviderCode),
            MediaType: mediaType,
            Title: candidate.Title,
            DownloadUri: candidate.DownloadUri,
            SourceUrl: candidate.SourceUrl,
            Seeders: candidate.Seeders,
            SizeBytes: candidate.SizeBytes,
            SourceProviderCode: string.IsNullOrWhiteSpace(candidate.SourceProviderCode) ? provider.ProviderCode : candidate.SourceProviderCode,
            SourceProviderName: string.IsNullOrWhiteSpace(candidate.SourceProviderName) ? provider.ProviderName : candidate.SourceProviderName);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<DownloadCandidateSearchRequest>> BuildSearchRequests(SearchBookDetailsResponse details)
    {
        var author = details.Authors.FirstOrDefault();
        var jackettQueries = new List<DownloadCandidateSearchRequest>();
        var flibustaQueries = new List<DownloadCandidateSearchRequest>();

        if (!string.IsNullOrWhiteSpace(details.Title) && !string.IsNullOrWhiteSpace(author))
        {
            jackettQueries.Add(new DownloadCandidateSearchRequest($"{details.Title} {author}"));
        }

        if (!string.IsNullOrWhiteSpace(details.Title))
        {
            var normalized = NormalizeQueryTerm(details.Title);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                jackettQueries.Add(new DownloadCandidateSearchRequest(normalized));
                flibustaQueries.Add(new DownloadCandidateSearchRequest(normalized, author));
            }
        }

        if (!string.IsNullOrWhiteSpace(details.OriginalTitle) && !string.IsNullOrWhiteSpace(author))
        {
            jackettQueries.Add(new DownloadCandidateSearchRequest($"{details.OriginalTitle} {author}"));
        }

        if (!string.IsNullOrWhiteSpace(details.OriginalTitle))
        {
            var normalized = NormalizeQueryTerm(details.OriginalTitle);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                flibustaQueries.Add(new DownloadCandidateSearchRequest(normalized, author));
            }
        }

        return new Dictionary<string, IReadOnlyList<DownloadCandidateSearchRequest>>(StringComparer.OrdinalIgnoreCase)
        {
            ["jackett"] = DistinctRequests(jackettQueries),
            ["flibusta"] = DistinctRequests(flibustaQueries),
        };
    }

    private static IReadOnlyList<DownloadCandidateSearchRequest> GetSearchRequests(
        IDownloadCandidateProvider provider,
        IReadOnlyDictionary<string, IReadOnlyList<DownloadCandidateSearchRequest>> requestsByProvider)
    {
        if (requestsByProvider.TryGetValue(provider.ProviderCode, out var requests) && requests.Count > 0)
        {
            return requests;
        }

        return Array.Empty<DownloadCandidateSearchRequest>();
    }

    private static IReadOnlyList<DownloadCandidateSearchRequest> DistinctRequests(
        IReadOnlyList<DownloadCandidateSearchRequest> requests)
    {
        return requests
            .Select(x => new DownloadCandidateSearchRequest(
                NormalizeQueryTerm(x.Query),
                string.IsNullOrWhiteSpace(x.AuthorFilter) ? null : NormalizeQueryTerm(x.AuthorFilter)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Query))
            .DistinctBy(x => $"{x.Query}\n{x.AuthorFilter}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static string BuildCandidateId(DownloadCandidateRaw candidate, string providerCode)
    {
        var uniqueIdentifier = candidate.UniqueIdentifier?.Trim();
        var suffix = !string.IsNullOrWhiteSpace(uniqueIdentifier)
            ? uniqueIdentifier
            : $"{candidate.DownloadUri}|{candidate.SourceUrl}";
        var sourceProviderCode = string.IsNullOrWhiteSpace(candidate.SourceProviderCode)
            ? providerCode
            : candidate.SourceProviderCode.Trim();

        return $"{sourceProviderCode}:{suffix}";
    }

    private static string GetExecutionProviderCode(string candidateId)
    {
        return candidateId.StartsWith("flibusta:", StringComparison.OrdinalIgnoreCase)
            ? "local-http-epub"
            : "qbittorrent";
    }

    private static string GetErrorCode(string providerCode)
    {
        return providerCode.Equals("flibusta", StringComparison.OrdinalIgnoreCase)
            ? "FLIBUSTA_UNAVAILABLE"
            : providerCode.Equals("jackett", StringComparison.OrdinalIgnoreCase)
                ? "JACKETT_UNAVAILABLE"
                : CandidateProviderUnavailableCode;
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

    private sealed record ProviderGroupResult(
        IDownloadCandidateProvider Provider,
        int Total,
        IReadOnlyList<DownloadCandidateDto> Items,
        Exception? Error);

    private sealed record PagedProviderGroupResult(
        IDownloadCandidateProvider Provider,
        int Total,
        string? ErrorCode,
        string? ErrorMessage,
        IReadOnlyList<DownloadCandidateDto> Items);
}
