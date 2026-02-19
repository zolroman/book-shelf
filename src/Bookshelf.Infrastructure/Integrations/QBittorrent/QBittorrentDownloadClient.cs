using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Text.Json;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookshelf.Infrastructure.Integrations.QBittorrent;

public sealed class QBittorrentDownloadClient : IDownloadExecutionClient
{
    private const string ProviderCode = "qbittorrent";
    public const string MeterName = "Bookshelf.Integrations.QBittorrent";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>("qbittorrent_requests_total");
    private static readonly Counter<long> FailureCounter = Meter.CreateCounter<long>("qbittorrent_failures_total");
    private static readonly Histogram<double> LatencyHistogram = Meter.CreateHistogram<double>("qbittorrent_request_duration_ms");

    private readonly HttpClient _httpClient;
    private readonly ILogger<QBittorrentDownloadClient> _logger;
    private readonly QBittorrentOptions _options;

    public QBittorrentDownloadClient(
        HttpClient httpClient,
        IOptions<QBittorrentOptions> options,
        ILogger<QBittorrentDownloadClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public TimeSpan NotFoundGracePeriod => TimeSpan.FromSeconds(Math.Max(1, _options.NotFoundGraceSeconds));

    public async Task<DownloadEnqueueResult> EnqueueAsync(
        string downloadUri,
        string candidateId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUri))
        {
            throw new DownloadExecutionFailedException(ProviderCode, "downloadUri is required.");
        }

        if (string.IsNullOrWhiteSpace(candidateId))
        {
            throw new DownloadExecutionFailedException(ProviderCode, "candidateId is required.");
        }

        var normalizedDownloadUri = downloadUri.Trim();
        var normalizedCandidateId = candidateId.Trim();

        using var response = await SendWithRetryAsync(
            operation: "enqueue",
            requestFactory: () => new HttpRequestMessage(HttpMethod.Post, "/api/v2/torrents/add")
            {
                Content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("urls", normalizedDownloadUri),
                    new KeyValuePair<string, string>("tags", normalizedCandidateId),
                    new KeyValuePair<string, string>("category", "book"),
                    new KeyValuePair<string, string>("savepath", "books"),
                ]),
            },
            cancellationToken);

        return new DownloadEnqueueResult(normalizedCandidateId);
    }

    public async Task<DownloadStatusResult> GetStatusAsync(
        string externalJobId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalJobId))
        {
            return new DownloadStatusResult(ExternalDownloadState.NotFound, null, null);
        }

        var items = await GetTorrentsByTagAsync(externalJobId.Trim(), cancellationToken);
        var item = items
            .OrderByDescending(x => x.AddedAtUtc ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
        if (item is null)
        {
            return new DownloadStatusResult(ExternalDownloadState.NotFound, null, null);
        }

        return new DownloadStatusResult(
            State: MapState(item.State),
            StoragePath: item.ContentPath ?? item.SavePath,
            SizeBytes: item.SizeBytes ?? item.TotalSizeBytes);
    }

    public async Task CancelAsync(
        string externalJobId,
        bool deleteFiles,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalJobId))
        {
            throw new DownloadExecutionFailedException(ProviderCode, "externalJobId is required for cancellation.");
        }

        var torrents = await GetTorrentsByTagAsync(externalJobId.Trim(), cancellationToken);
        var hashes = torrents
            .Select(x => x.Hash)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (hashes.Length == 0)
        {
            _logger.LogInformation(
                "qBittorrent cancel skipped. No torrents found for tag {Tag}.",
                externalJobId.Trim());
            return;
        }

        using var response = await SendWithRetryAsync(
            operation: "cancel",
            requestFactory: () => new HttpRequestMessage(HttpMethod.Post, "/api/v2/torrents/delete")
            {
                Content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("hashes", string.Join('|', hashes)),
                    new KeyValuePair<string, string>("deleteFiles", deleteFiles ? "true" : "false"),
                ]),
            },
            cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        string operation,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        var attempts = Math.Max(0, _options.MaxRetries) + 1;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            RequestCounter.Add(1, new("provider", ProviderCode), new("operation", operation));
            try
            {
                var request = requestFactory();
                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    if (IsTransientStatusCode(response.StatusCode))
                    {
                        response.Dispose();
                        throw new HttpRequestException(
                            $"qBittorrent transient status code {(int)response.StatusCode}.");
                    }

                    response.Dispose();
                    throw new DownloadExecutionFailedException(
                        ProviderCode,
                        $"qBittorrent {operation} returned non-success status code {(int)response.StatusCode}.");
                }

                _logger.LogInformation(
                    "qBittorrent {Operation} completed. Attempt={Attempt} DurationMs={DurationMs}",
                    operation,
                    attempt,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                LogLatency(startedAt, operation, success: true);

                return response;
            }
            catch (DownloadExecutionFailedException)
            {
                throw;
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                lastException = exception;
                FailureCounter.Add(1, new("provider", ProviderCode), new("operation", operation));
                LogLatency(startedAt, operation, success: false);
                _logger.LogWarning(
                    exception,
                    "qBittorrent {Operation} transient failure. Attempt={Attempt}/{Attempts}",
                    operation,
                    attempt,
                    attempts);

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
                FailureCounter.Add(1, new("provider", ProviderCode), new("operation", operation));
                LogLatency(startedAt, operation, success: false);
                throw new DownloadExecutionFailedException(
                    ProviderCode,
                    $"qBittorrent {operation} failed.",
                    exception);
            }
        }

        throw new DownloadExecutionUnavailableException(
            ProviderCode,
            $"qBittorrent {operation} is unavailable after retry attempts.",
            lastException);
    }

    private async Task<IReadOnlyList<QBittorrentTorrentInfo>> GetTorrentsByTagAsync(
        string candidateTag,
        CancellationToken cancellationToken)
    {
        var encodedTag = Uri.EscapeDataString(candidateTag);
        using var response = await SendWithRetryAsync(
            operation: "tag_lookup",
            requestFactory: () => new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/v2/torrents/info?tag={encodedTag}&sort=added_on&reverse=true&limit=200"),
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return [];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException exception)
        {
            throw new DownloadExecutionFailedException(
                ProviderCode,
                "qBittorrent tag lookup payload is not valid JSON.",
                exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var items = new List<QBittorrentTorrentInfo>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var hash = TryGetString(item, "hash");
                if (string.IsNullOrWhiteSpace(hash))
                {
                    continue;
                }

                DateTimeOffset? addedAtUtc = null;
                var addedOnSeconds = TryGetInt64(item, "added_on");
                if (addedOnSeconds.HasValue && addedOnSeconds.Value > 0)
                {
                    try
                    {
                        addedAtUtc = DateTimeOffset.FromUnixTimeSeconds(addedOnSeconds.Value);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        addedAtUtc = null;
                    }
                }

                items.Add(new QBittorrentTorrentInfo(
                    Hash: hash.Trim().ToLowerInvariant(),
                    AddedAtUtc: addedAtUtc,
                    State: TryGetString(item, "state"),
                    ContentPath: TryGetString(item, "content_path"),
                    SavePath: TryGetString(item, "save_path"),
                    SizeBytes: TryGetInt64(item, "size"),
                    TotalSizeBytes: TryGetInt64(item, "total_size")));
            }

            return items;
        }
    }

    private static ExternalDownloadState MapState(string? stateRaw)
    {
        if (string.IsNullOrWhiteSpace(stateRaw))
        {
            return ExternalDownloadState.Downloading;
        }

        var state = stateRaw.Trim().ToLowerInvariant();

        if (state.Contains("error", StringComparison.Ordinal) || state == "missingfiles")
        {
            return ExternalDownloadState.Failed;
        }

        if (state is "queueddl" or "metadl" or "pauseddl" or "checkingdl")
        {
            return ExternalDownloadState.Queued;
        }

        if (state is "downloading" or "stalleddl" or "forceddl" or "moving" or "allocating")
        {
            return ExternalDownloadState.Downloading;
        }

        if (state is "uploading" or "stalledup" or "forcedup" or "pausedup" or "queuedup" or "checkingup")
        {
            return ExternalDownloadState.Completed;
        }

        return ExternalDownloadState.Downloading;
    }

    private static string? TryGetString(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.GetString();
    }

    private static long? TryGetInt64(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.TryGetInt64(out var parsed) ? parsed : null;
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

    private static void LogLatency(long startTimestamp, string operation, bool success)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LatencyHistogram.Record(
            elapsedMs,
            new("provider", ProviderCode),
            new("operation", operation),
            new("success", success.ToString().ToLowerInvariant()));
    }

    private sealed record QBittorrentTorrentInfo(
        string Hash,
        DateTimeOffset? AddedAtUtc,
        string? State,
        string? ContentPath,
        string? SavePath,
        long? SizeBytes,
        long? TotalSizeBytes);
}
