namespace Bookshelf.Application.Abstractions.Providers;

public sealed record MetadataSearchRequest(
    string? Title,
    string? Author,
    int Page,
    int PageSize);

public sealed record MetadataSeriesInfo(
    string ProviderSeriesKey,
    string Title,
    int Order);

public sealed record MetadataSearchItem(
    string ProviderBookKey,
    string Title,
    IReadOnlyList<string> Authors,
    MetadataSeriesInfo? Series);

public sealed record MetadataSearchResult(
    int Total,
    IReadOnlyList<MetadataSearchItem> Items);

public sealed record MetadataBookDetails(
    string ProviderBookKey,
    string Title,
    string? OriginalTitle,
    string? Description,
    int? PublishYear,
    string? CoverUrl,
    IReadOnlyList<string> Authors,
    MetadataSeriesInfo? Series);
