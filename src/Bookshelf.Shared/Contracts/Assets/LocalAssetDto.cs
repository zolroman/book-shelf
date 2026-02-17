namespace Bookshelf.Shared.Contracts.Assets;

public sealed record LocalAssetDto(
    int Id,
    int UserId,
    int BookFormatId,
    string LocalPath,
    long FileSizeBytes,
    DateTime DownloadedAtUtc,
    DateTime? DeletedAtUtc);
