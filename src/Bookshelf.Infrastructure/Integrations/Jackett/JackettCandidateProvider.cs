using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookshelf.Infrastructure.Integrations.Jackett;

public sealed class JackettCandidateProvider : IDownloadCandidateProvider
{
    public const string MeterName = "Bookshelf.Integrations.Jackett";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>("jackett_requests_total");
    private static readonly Counter<long> FailureCounter = Meter.CreateCounter<long>("jackett_failures_total");
    private static readonly Histogram<double> LatencyHistogram = Meter.CreateHistogram<double>("jackett_request_duration_ms");
    private static readonly JsonSerializerOptions JackettJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<JackettCandidateProvider> _logger;
    private readonly JackettOptions _options;

    public JackettCandidateProvider(
        HttpClient httpClient,
        IOptions<JackettOptions> options,
        ILogger<JackettCandidateProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public string ProviderCode => "jackett";

    public async Task<IReadOnlyList<DownloadCandidateRaw>> SearchAsync(
        string query,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new DownloadCandidateProviderUnavailableException(ProviderCode, "Jackett API key is not configured.");
        }

        var uri = BuildUri(query);
        var payload = await SendWithRetryAsync(uri, cancellationToken);
        var parsed = ParsePayload(payload);
        var limit = Math.Min(Math.Max(1, maxItems), Math.Max(1, _options.MaxItems));
        return parsed.Take(limit).ToArray();
    }

    private async Task<string> SendWithRetryAsync(Uri uri, CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        var redactedUri = RedactUri(uri);
        var attempts = Math.Max(0, _options.MaxRetries) + 1;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            RequestCounter.Add(1, new("provider", ProviderCode), new("operation", "search"));
            try
            {
                using var response = await _httpClient.GetAsync(uri, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    if (IsTransientStatusCode(response.StatusCode))
                    {
                        throw new HttpRequestException(
                            $"Jackett transient status code {(int)response.StatusCode}.");
                    }

                    throw new DownloadCandidateProviderUnavailableException(
                        ProviderCode,
                        $"Jackett returned non-success status code {(int)response.StatusCode}.");
                }

                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    throw new InvalidOperationException("Jackett returned an empty payload.");
                }

                _logger.LogInformation(
                    "Jackett search completed. Attempt={Attempt} Url={Url} DurationMs={DurationMs}",
                    attempt,
                    redactedUri,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                LogLatency(startedAt, success: true);

                return payload;
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                lastException = exception;
                FailureCounter.Add(1, new("provider", ProviderCode), new("operation", "search"));
                LogLatency(startedAt, success: false);
                _logger.LogWarning(
                    exception,
                    "Jackett transient failure. Attempt={Attempt}/{Attempts} Url={Url}",
                    attempt,
                    attempts,
                    redactedUri);

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
                FailureCounter.Add(1, new("provider", ProviderCode), new("operation", "search"));
                LogLatency(startedAt, success: false);
                _logger.LogError(exception, "Jackett non-transient failure. Url={Url}", redactedUri);
                break;
            }
        }

        throw new DownloadCandidateProviderUnavailableException(
            ProviderCode,
            "Jackett is unavailable after retry attempts.",
            lastException);
    }

    private IReadOnlyList<DownloadCandidateRaw> ParsePayload(string jsonPayload)
    {
        JackettSearchResponseContract? payload;
        try
        {
            payload = JsonSerializer.Deserialize<JackettSearchResponseContract>(jsonPayload, JackettJsonOptions);
        }
        catch (JsonException exception)
        {
            throw new DownloadCandidateProviderUnavailableException(ProviderCode, "Jackett response is not valid JSON.", exception);
        }

        if (payload?.Results is null || payload.Results.Count == 0)
        {
            return Array.Empty<DownloadCandidateRaw>();
        }

        var candidates = new List<DownloadCandidateRaw>();
        var seenIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in payload.Results)
        {
            var title = NormalizeText(item.Title);
            var guid = NormalizeText(item.Guid);
            var magnetUrl = NormalizeText(item.MagnetUri);
            var link = NormalizeText(item.Link);
            var detailsUrl = NormalizeText(item.Details);

            var downloadUri = !string.IsNullOrWhiteSpace(magnetUrl) ? magnetUrl : link;
            var sourceUrl = !string.IsNullOrWhiteSpace(detailsUrl) ? detailsUrl : guid;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(downloadUri))
            {
                continue;
            }

            var uniqueIdentifier = !string.IsNullOrWhiteSpace(guid)
                ? guid
                : $"{downloadUri}|{sourceUrl}";
            if (!seenIdentifiers.Add(uniqueIdentifier))
            {
                continue;
            }

            candidates.Add(new DownloadCandidateRaw(
                Title: title,
                DownloadUri: downloadUri.Trim(),
                SourceUrl: string.IsNullOrWhiteSpace(sourceUrl) ? downloadUri.Trim() : sourceUrl.Trim(),
                Seeders: item.Seeders,
                SizeBytes: item.Size,
                PublishedAtUtc: item.PublishDate?.ToUniversalTime(),
                UniqueIdentifier: uniqueIdentifier));
        }

        return candidates;
    }

    private Uri BuildUri(string query)
    {
        var normalizedQuery = string.Join(' ', query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var encodedQuery = Uri.EscapeDataString(normalizedQuery);
        var encodedApiKey = Uri.EscapeDataString(_options.ApiKey);
        var relative = $"/api/v2.0/indexers/all/results?apikey={encodedApiKey}&Query={encodedQuery}";
        return new Uri(relative, UriKind.Relative);
    }

    private static bool IsTransient(Exception exception)
    {
        return exception is HttpRequestException or TaskCanceledException or TimeoutException;
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500;
    }

    private static void LogLatency(long startTimestamp, bool success)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LatencyHistogram.Record(
            elapsedMs,
            new("provider", "jackett"),
            new("operation", "search"),
            new("success", success.ToString().ToLowerInvariant()));
    }

    private string RedactUri(Uri uri)
    {
        var raw = uri.ToString();
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return raw;
        }

        var encodedApiKey = Uri.EscapeDataString(_options.ApiKey);
        return raw
            .Replace(_options.ApiKey, "***", StringComparison.Ordinal)
            .Replace(encodedApiKey, "***", StringComparison.Ordinal);
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class JackettSearchResponseContract
    {
        [JsonPropertyName("Results")]
        public List<JackettResultContract>? Results { get; init; }
    }

    private sealed class JackettResultContract
    {
        [JsonPropertyName("Title")]
        public string? Title { get; init; }

        [JsonPropertyName("Guid")]
        public string? Guid { get; init; }

        [JsonPropertyName("Link")]
        public string? Link { get; init; }

        [JsonPropertyName("Details")]
        public string? Details { get; init; }

        [JsonPropertyName("MagnetUri")]
        public string? MagnetUri { get; init; }

        [JsonPropertyName("Seeders")]
        public int? Seeders { get; init; }

        [JsonPropertyName("Size")]
        public long? Size { get; init; }

        [JsonPropertyName("PublishDate")]
        public DateTimeOffset? PublishDate { get; init; }
    }
}
