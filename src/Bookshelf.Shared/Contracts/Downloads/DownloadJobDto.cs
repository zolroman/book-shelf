namespace Bookshelf.Shared.Contracts.Downloads;

public sealed record DownloadJobDto(
    int Id,
    int UserId,
    int BookFormatId,
    string Status,
    string Source,
    string ExternalJobId,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);
