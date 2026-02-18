namespace Bookshelf.Shared.Client;

public sealed record LocalMediaEntry(
    long UserId,
    long BookId,
    string MediaType,
    string LocalPath,
    bool IsAvailable,
    DateTimeOffset UpdatedAtUtc);

public interface ILocalMediaIndexService
{
    Task<LocalMediaEntry?> GetAsync(
        long bookId,
        string mediaType,
        CancellationToken cancellationToken = default);
}
