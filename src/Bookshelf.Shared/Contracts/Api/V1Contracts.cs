namespace Bookshelf.Shared.Contracts.Api;

public sealed record ErrorResponse(
    string Code,
    string Message,
    object? Details,
    string CorrelationId);

public sealed record SearchBooksQuery(
    string? Title,
    string? Author);

public sealed record SearchSeriesDto(
    string ProviderSeriesKey,
    string Title,
    int Order);

public sealed record SearchBookItemDto(
    string ProviderCode,
    string ProviderBookKey,
    string Title,
    IReadOnlyList<string> Authors,
    SearchSeriesDto? Series,
    bool InCatalog,
    string CatalogState);

public sealed record SearchBooksResponse(
    SearchBooksQuery Query,
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<SearchBookItemDto> Items);

public sealed record SearchBookDetailsResponse(
    string ProviderCode,
    string ProviderBookKey,
    string Title,
    string? OriginalTitle,
    string? Description,
    int? PublishYear,
    string? CoverUrl,
    IReadOnlyList<string> Authors,
    SearchSeriesDto? Series);

public sealed record DownloadCandidateDto(
    string CandidateId,
    string MediaType,
    string Title,
    string DownloadUri,
    string SourceUrl,
    int? Seeders,
    long? SizeBytes);

public sealed record DownloadCandidatesResponse(
    string ProviderCode,
    string ProviderBookKey,
    string MediaType,
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<DownloadCandidateDto> Items);

public sealed record LibraryBookDto(
    long Id,
    string ProviderCode,
    string ProviderBookKey,
    string Title,
    string? OriginalTitle,
    string? Description,
    int? PublishYear,
    string? LanguageCode,
    string? CoverUrl,
    string CatalogState,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record LibraryResponse(
    int Page,
    int PageSize,
    int Total,
    bool IncludeArchived,
    IReadOnlyList<LibraryBookDto> Items);

public sealed record AddAndDownloadRequest(
    long UserId,
    string ProviderCode,
    string ProviderBookKey,
    string MediaType,
    string CandidateId);

public sealed record DownloadJobSummaryDto(
    long Id,
    string Status,
    string? ExternalJobId,
    DateTimeOffset CreatedAtUtc);

public sealed record AddAndDownloadResponse(
    long BookId,
    string BookState,
    DownloadJobSummaryDto DownloadJob);

public sealed record DownloadJobDto(
    long Id,
    long UserId,
    long BookId,
    string MediaType,
    string Status,
    string? ExternalJobId,
    string? FailureReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record DownloadJobsResponse(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<DownloadJobDto> Items);

public sealed record CancelDownloadJobRequest(
    long UserId);

public sealed record ShelfDto(
    long Id,
    long UserId,
    string Name,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<long> BookIds);

public sealed record ShelvesResponse(
    IReadOnlyList<ShelfDto> Items);

public sealed record CreateShelfRequest(
    long UserId,
    string Name);

public sealed record CreateShelfResponse(
    ShelfDto Shelf);

public sealed record AddBookToShelfRequest(
    long UserId,
    long BookId);

public sealed record AddBookToShelfResponse(
    ShelfDto Shelf);
