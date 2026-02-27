namespace Bookshelf.Application.Abstractions.Providers;

public static class MetadataSearchItemKinds
{
    public const string Book = "book";
    public const string Series = "series";
}

public sealed record MetadataSearchRequest(
    string? Title,
    string? Author,
    int Page);

public sealed record MetadataSeriesInfo(
    string ProviderSeriesKey,
    string Title,
    int Order);

public sealed record MetadataSearchItem(
    string ProviderBookKey,
    string Title,
    IReadOnlyList<string> Authors,
    MetadataSeriesInfo? Series,
    string Kind = MetadataSearchItemKinds.Book,
    string? ProviderSeriesKey = null);

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

public sealed record MetadataSeriesBookItem(
    int Order,
    string ProviderBookKey,
    string Title,
    IReadOnlyList<string> Authors,
    int? PublishYear);

public sealed record MetadataSeriesDetails(
    string ProviderSeriesKey,
    string Title,
    IReadOnlyList<MetadataSeriesBookItem> Items);
