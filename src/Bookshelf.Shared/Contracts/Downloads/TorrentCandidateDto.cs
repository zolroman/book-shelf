namespace Bookshelf.Shared.Contracts.Downloads;

public sealed record TorrentCandidateDto(
    string Title,
    string DownloadUri,
    string Source,
    int Seeders,
    long? SizeBytes);
