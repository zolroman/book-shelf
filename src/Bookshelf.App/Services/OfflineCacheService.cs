using System.Text.Json;

namespace Bookshelf.App.Services;

public sealed class OfflineCacheService(IOfflineStateStore stateStore) : IOfflineCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IOfflineStateStore _stateStore = stateStore;

    public async Task SaveAsync<T>(string key, T payload, CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeKey(key);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await _stateStore.SaveMetadataAsync(normalizedKey, json, cancellationToken);
    }

    public async Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeKey(key);
        var json = await _stateStore.LoadMetadataAsync(normalizedKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string NormalizeKey(string key)
    {
        return key.Trim().Replace("/", "_").Replace("\\", "_");
    }
}
