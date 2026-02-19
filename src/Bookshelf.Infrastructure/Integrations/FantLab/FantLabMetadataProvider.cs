using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookshelf.Infrastructure.Integrations.FantLab;

public sealed class FantLabMetadataProvider : IMetadataProvider
{
    public const string MeterName = "Bookshelf.Integrations.FantLab";

    private const string RequestTypeSearch = "search";
    private const string RequestTypeDetails = "details";
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>("fantlab_requests_total");
    private static readonly Counter<long> FailureCounter = Meter.CreateCounter<long>("fantlab_failures_total");
    private static readonly Histogram<double> LatencyHistogram = Meter.CreateHistogram<double>("fantlab_request_duration_ms");

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<FantLabMetadataProvider> _logger;
    private readonly FantLabOptions _options;
    private readonly object _circuitLock = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _openUntilUtc;

    public FantLabMetadataProvider(
        HttpClient httpClient,
        IMemoryCache memoryCache,
        IOptions<FantLabOptions> options,
        ILogger<FantLabMetadataProvider> logger)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _logger = logger;
        _options = options.Value;
    }

    public string ProviderCode => "fantlab";

    public async Task<MetadataSearchResult> SearchAsync(
        MetadataSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedTitle = NormalizeQueryTerm(request.Title);
        var normalizedAuthor = NormalizeQueryTerm(request.Author);
        var cacheKey = $"fantlab:search:{normalizedTitle}:{normalizedAuthor}:page:{request.Page}";

        if (_options.CacheEnabled && _memoryCache.TryGetValue(cacheKey, out MetadataSearchResult? cachedResult))
        {
            return cachedResult!;
        }

        if (IsCircuitOpen())
        {
            throw new MetadataProviderUnavailableException(
                ProviderCode,
                "FantLab circuit is open and cache does not contain requested search data.");
        }

        var uri = BuildSearchUri(normalizedTitle, normalizedAuthor, request.Page);
        var payload = await SendWithRetryAsync(uri, RequestTypeSearch, cancellationToken);
        var parsed = ParseSearchPayload(payload);

        if (_options.CacheEnabled)
        {
            _memoryCache.Set(cacheKey, parsed, TimeSpan.FromMinutes(_options.SearchCacheMinutes));
        }

        return parsed;
    }

    public async Task<MetadataBookDetails?> GetDetailsAsync(
        string providerBookKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerBookKey))
        {
            throw new ArgumentException("Provider book key is required.", nameof(providerBookKey));
        }

        var normalizedBookKey = providerBookKey.Trim();
        var cacheKey = $"fantlab:book:{normalizedBookKey}";

        if (_options.CacheEnabled && _memoryCache.TryGetValue(cacheKey, out MetadataBookDetails? cachedResult))
        {
            return cachedResult;
        }

        if (IsCircuitOpen())
        {
            throw new MetadataProviderUnavailableException(
                ProviderCode,
                "FantLab circuit is open and cache does not contain requested details.");
        }

        var uri = BuildDetailsUri(normalizedBookKey);
        var payload = await SendWithRetryAsync(uri, RequestTypeDetails, cancellationToken);
        var parsed = ParseDetailsPayload(payload, normalizedBookKey);

        if (parsed is not null && _options.CacheEnabled)
        {
            _memoryCache.Set(cacheKey, parsed, TimeSpan.FromHours(_options.DetailsCacheHours));
        }

        return parsed;
    }

    private async Task<string> SendWithRetryAsync(Uri uri, string requestType, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            throw new MetadataProviderUnavailableException(ProviderCode, "FantLab integration is disabled.");
        }

        Exception? lastException = null;
        var attempts = Math.Max(0, _options.MaxRetries) + 1;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var start = Stopwatch.GetTimestamp();
            RequestCounter.Add(1, new("provider", ProviderCode), new("request_type", requestType));
            try
            {
                using var response = await _httpClient.GetAsync(uri, cancellationToken);
                response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    throw new InvalidOperationException("FantLab returned an empty payload.");
                }

                OnRequestSuccess();
                LogLatency(start, requestType, success: true);
                _logger.LogInformation(
                    "FantLab request completed. Provider={Provider} RequestType={RequestType} Attempt={Attempt} Url={Url}",
                    ProviderCode,
                    requestType,
                    attempt,
                    uri);

                return payload;
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                lastException = exception;
                FailureCounter.Add(1, new("provider", ProviderCode), new("request_type", requestType));
                LogLatency(start, requestType, success: false);

                _logger.LogWarning(
                    exception,
                    "FantLab transient failure. Provider={Provider} RequestType={RequestType} Attempt={Attempt}/{Attempts} Url={Url}",
                    ProviderCode,
                    requestType,
                    attempt,
                    attempts,
                    uri);

                if (attempt >= attempts)
                {
                    break;
                }

                var delay = TimeSpan.FromMilliseconds(
                    (_options.RetryDelayMs * Math.Pow(2, attempt - 1)) + Random.Shared.Next(0, 120));

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception exception)
            {
                lastException = exception;
                FailureCounter.Add(1, new("provider", ProviderCode), new("request_type", requestType));
                LogLatency(start, requestType, success: false);

                _logger.LogError(
                    exception,
                    "FantLab non-transient failure. Provider={Provider} RequestType={RequestType} Url={Url}",
                    ProviderCode,
                    requestType,
                    uri);
                break;
            }
        }

        OnRequestFailure();
        throw new MetadataProviderUnavailableException(
            ProviderCode,
            "FantLab is unavailable after retry attempts.",
            lastException);
    }

    private MetadataSearchResult ParseSearchPayload(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var itemsNode = FindArrayNode(root, "items", "results", "works", "books", "data") ?? (root.ValueKind == JsonValueKind.Array ? root : default);

        if (itemsNode.ValueKind != JsonValueKind.Array)
        {
            throw new MetadataProviderUnavailableException(ProviderCode, "FantLab search payload does not contain an items array.");
        }

        var items = new List<MetadataSearchItem>();
        foreach (var element in itemsNode.EnumerateArray())
        {
            var providerBookKey = GetStringValue(element, "providerBookKey", "bookId", "work_id", "workId", "id");
            var title = GetStringValue(element, "rusname", "title", "work_name", "workName", "fullname", "name", "altname");
            title = NormalizeDisplayText(title);
            if (string.IsNullOrWhiteSpace(providerBookKey) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var authors = GetSearchAuthors(element);
            var series = ParseSeries(element);
            items.Add(new MetadataSearchItem(
                ProviderBookKey: providerBookKey,
                Title: title,
                Authors: authors,
                Series: series));
        }

        if (items.Count == 0)
        {
            throw new MetadataProviderUnavailableException(ProviderCode, "FantLab search payload did not include minimally valid items.");
        }

        var total = GetIntValue(root, "total", "count") ?? itemsNode.GetArrayLength();
        return new MetadataSearchResult(total, items);
    }

    private MetadataBookDetails? ParseDetailsPayload(string payload, string requestedBookKey)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var node = FindObjectNode(root, "item", "book", "work", "data") ?? root;
        if (node.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var providerBookKey = GetStringValue(node, "providerBookKey", "bookId", "work_id", "workId", "id") ?? requestedBookKey;
        var title = GetStringValue(node, "title", "name", "work_name", "workName");
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var authors = GetStringList(node, "authors", "author", "writers");
        var series = ParseSeries(node);

        return new MetadataBookDetails(
            ProviderBookKey: providerBookKey,
            Title: title,
            OriginalTitle: GetStringValue(node, "originalTitle", "orig_title", "original_name"),
            Description: GetStringValue(node, "description", "annotation", "about"),
            PublishYear: GetIntValue(node, "publishYear", "year", "publicationYear"),
            CoverUrl: GetStringValue(node, "coverUrl", "cover", "image", "cover_url"),
            Authors: authors,
            Series: series);
    }

    private MetadataSeriesInfo? ParseSeries(JsonElement node)
    {
        JsonElement seriesNode;
        if (TryGetProperty(node, out seriesNode, "series") && seriesNode.ValueKind == JsonValueKind.Object)
        {
            var key = GetStringValue(seriesNode, "providerSeriesKey", "id", "series_id", "seriesId");
            var title = GetStringValue(seriesNode, "title", "name");
            var order = GetIntValue(seriesNode, "order", "series_order", "number");
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(title) && order.HasValue && order.Value > 0)
            {
                return new MetadataSeriesInfo(key, title, order.Value);
            }
        }

        if (TryGetProperty(node, out seriesNode, "serieses", "series_list", "seriesList") &&
            seriesNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in seriesNode.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var key = GetStringValue(item, "providerSeriesKey", "id", "series_id", "seriesId");
                var title = GetStringValue(item, "title", "name");
                var order = GetIntValue(item, "order", "series_order", "number");
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(title) && order.HasValue && order.Value > 0)
                {
                    return new MetadataSeriesInfo(key, title, order.Value);
                }
            }
        }

        return null;
    }

    private Uri BuildSearchUri(string? title, string? author, int page)
    {
        var queryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(title))
        {
            queryParts.Add($"q={Uri.EscapeDataString(title)}");
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            queryParts.Add($"author={Uri.EscapeDataString(author)}");
        }

        queryParts.Add($"page={page}");
        queryParts.Add("onlymatches=1");

        var path = _options.SearchPath.StartsWith('/') ? _options.SearchPath : $"/{_options.SearchPath}";
        var query = string.Join("&", queryParts);
        return new Uri($"{path}?{query}", UriKind.Relative);
    }

    private Uri BuildDetailsUri(string bookKey)
    {
        var pathTemplate = _options.BookDetailsPath.StartsWith('/') ? _options.BookDetailsPath : $"/{_options.BookDetailsPath}";
        if (pathTemplate.Contains("{bookKey}", StringComparison.Ordinal))
        {
            pathTemplate = pathTemplate.Replace("{bookKey}", Uri.EscapeDataString(bookKey), StringComparison.Ordinal);
            return new Uri(pathTemplate, UriKind.Relative);
        }

        return new Uri($"{pathTemplate}?bookKey={Uri.EscapeDataString(bookKey)}", UriKind.Relative);
    }

    private bool IsCircuitOpen()
    {
        lock (_circuitLock)
        {
            return _openUntilUtc.HasValue && _openUntilUtc.Value > DateTimeOffset.UtcNow;
        }
    }

    private void OnRequestSuccess()
    {
        lock (_circuitLock)
        {
            _consecutiveFailures = 0;
            _openUntilUtc = null;
        }
    }

    private void OnRequestFailure()
    {
        lock (_circuitLock)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= Math.Max(1, _options.CircuitBreakerFailureThreshold))
            {
                _openUntilUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, _options.CircuitBreakerOpenSeconds));
            }
        }
    }

    private void LogLatency(long startTimestamp, string requestType, bool success)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LatencyHistogram.Record(elapsedMs, new("provider", ProviderCode), new("request_type", requestType), new("success", success.ToString().ToLowerInvariant()));
    }

    private static bool IsTransient(Exception exception)
    {
        return exception is HttpRequestException or TaskCanceledException or TimeoutException;
    }

    private static string? NormalizeQueryTerm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static JsonElement? FindArrayNode(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(root, out var node, name) && node.ValueKind == JsonValueKind.Array)
            {
                return node;
            }
        }

        return null;
    }

    private static JsonElement? FindObjectNode(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(root, out var node, name) && node.ValueKind == JsonValueKind.Object)
            {
                return node;
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                foreach (var name in names)
                {
                    if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }
        }

        value = default;
        return false;
    }

    private static string? GetStringValue(JsonElement node, params string[] names)
    {
        if (!TryGetProperty(node, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim(),
            JsonValueKind.Number => value.ToString(),
            _ => null,
        };
    }

    private static int? GetIntValue(JsonElement node, params string[] names)
    {
        if (!TryGetProperty(node, out var value, names))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
        {
            return numeric;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static IReadOnlyList<string> GetStringList(JsonElement node, params string[] names)
    {
        if (!TryGetProperty(node, out var value, names))
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                switch (item.ValueKind)
                {
                    case JsonValueKind.String:
                    {
                        var text = item.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            values.Add(text.Trim());
                        }

                        break;
                    }
                    case JsonValueKind.Object:
                    {
                        var text = GetStringValue(item, "name", "fullName", "title");
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            values.Add(text);
                        }

                        break;
                    }
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                values.Add(text.Trim());
            }
        }

        return values;
    }

    private static IReadOnlyList<string> GetSearchAuthors(JsonElement node)
    {
        var result = new List<string>();
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddSingle(string? rawValue)
        {
            var normalized = NormalizeDisplayText(rawValue);
            if (string.IsNullOrWhiteSpace(normalized) || !unique.Add(normalized))
            {
                return;
            }

            result.Add(normalized);
        }

        void AddMany(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return;
            }

            foreach (var candidate in rawValue.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries))
            {
                AddSingle(candidate);
            }
        }

        foreach (var author in GetStringList(node, "authors", "author", "writers"))
        {
            AddSingle(author);
        }

        AddMany(GetStringValue(node, "all_autor_rusname", "all_autor_name"));
        AddSingle(GetStringValue(node, "autor_rusname", "autor_name"));

        for (var index = 1; index <= 5; index++)
        {
            AddSingle(GetStringValue(node, $"autor{index}_rusname", $"autor{index}_name"));
        }

        return result;
    }

    private static string? NormalizeDisplayText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
