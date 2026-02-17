using System.Text.Json;

namespace Bookshelf.App.Services;

public sealed class OfflineCacheService : IOfflineCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public async Task SaveAsync<T>(string key, T payload, CancellationToken cancellationToken = default)
    {
        var path = BuildPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            await File.WriteAllTextAsync(path, json, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var path = BuildPath(key);
        if (!File.Exists(path))
        {
            return default;
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private static string BuildPath(string key)
    {
        var safeKey = key.Replace("/", "_").Replace("\\", "_");
        return Path.Combine(FileSystem.AppDataDirectory, "offline-cache", $"{safeKey}.json");
    }
}
