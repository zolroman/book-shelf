namespace Bookshelf.Infrastructure.Models;

public sealed record TorrentCandidate(
    string Title,
    string DownloadUri,
    string Source,
    int Seeders,
    long? SizeBytes);
