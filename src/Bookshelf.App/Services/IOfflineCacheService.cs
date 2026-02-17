namespace Bookshelf.App.Services;

public interface IOfflineCacheService
{
    Task SaveAsync<T>(string key, T payload, CancellationToken cancellationToken = default);

    Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default);
}
