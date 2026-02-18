using System.Diagnostics;
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
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUri))
        {
            throw new DownloadExecutionFailedException(ProviderCode, "downloadUri is required.");
        }

        using var response = await SendWithRetryAsync(
            operation: "enqueue",
            requestFactory: () =>
            {
                return new HttpRequestMessage(HttpMethod.Post, "/api/v2/torrents/add")
                {
                    Content = new FormUrlEncodedContent(
                    [
                        new KeyValuePair<string, string>("urls", downloadUri.Trim()),
                    ]),
                };
            },
            cancellationToken);

        return new DownloadEnqueueResult(TryExtractBtih(downloadUri));
    }

    public async Task<DownloadStatusResult> GetStatusAsync(
        string externalJobId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalJobId))
        {
            return new DownloadStatusResult(ExternalDownloadState.NotFound, null, null);
        }

        using var response = await SendWithRetryAsync(
            operation: "status",
            requestFactory: () =>
            {
                var encodedHash = Uri.EscapeDataString(externalJobId.Trim());
                return new HttpRequestMessage(
                    HttpMethod.Get,
                    $"/api/v2/torrents/info?hashes={encodedHash}");
            },
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new DownloadStatusResult(ExternalDownloadState.NotFound, null, null);
        }

        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            return new DownloadStatusResult(ExternalDownloadState.NotFound, null, null);
        }

        var item = document.RootElement[0];
        var stateRaw = TryGetString(item, "state");
        var storagePath = TryGetString(item, "content_path") ?? TryGetString(item, "save_path");
        var sizeBytes = TryGetInt64(item, "size") ?? TryGetInt64(item, "total_size");

        return new DownloadStatusResult(
            State: MapState(stateRaw),
            StoragePath: storagePath,
            SizeBytes: sizeBytes);
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

        using var response = await SendWithRetryAsync(
            operation: "cancel",
            requestFactory: () =>
            {
                return new HttpRequestMessage(HttpMethod.Post, "/api/v2/torrents/delete")
                {
                    Content = new FormUrlEncodedContent(
                    [
                        new KeyValuePair<string, string>("hashes", externalJobId.Trim()),
                        new KeyValuePair<string, string>("deleteFiles", deleteFiles ? "true" : "false"),
                    ]),
                };
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

                return response;
            }
            catch (DownloadExecutionFailedException)
            {
                throw;
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                lastException = exception;
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

    private static string? TryExtractBtih(string downloadUri)
    {
        if (!downloadUri.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        const string marker = "xt=urn:btih:";
        var markerIndex = downloadUri.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var valueStart = markerIndex + marker.Length;
        var valueEnd = downloadUri.IndexOf('&', valueStart);
        var hash = valueEnd >= 0
            ? downloadUri[valueStart..valueEnd]
            : downloadUri[valueStart..];

        if (string.IsNullOrWhiteSpace(hash))
        {
            return null;
        }

        return hash.Trim().ToLowerInvariant();
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
}
