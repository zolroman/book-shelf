namespace Bookshelf.Shared.Contracts.Assets;

public sealed record UpsertLocalAssetRequest(
    int UserId,
    int BookFormatId,
    string LocalPath,
    long FileSizeBytes);
