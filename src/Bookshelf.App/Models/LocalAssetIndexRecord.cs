namespace Bookshelf.App.Models;

public sealed record LocalAssetIndexRecord(
    int UserId,
    int BookFormatId,
    string LocalPath,
    long FileSizeBytes,
    DateTime DownloadedAtUtc,
    DateTime? DeletedAtUtc);
