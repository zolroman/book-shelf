namespace Bookshelf.Shared.Contracts.Downloads;

public sealed record StartDownloadRequest(
    int UserId,
    int BookFormatId,
    string Source);
