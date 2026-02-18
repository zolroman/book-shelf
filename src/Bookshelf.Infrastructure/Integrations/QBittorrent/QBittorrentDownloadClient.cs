using System.Diagnostics;
using System.Net;
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

    public async Task<DownloadEnqueueResult> EnqueueAsync(
        string downloadUri,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUri))
        {
            throw new DownloadExecutionFailedException(ProviderCode, "downloadUri is required.");
        }

        Exception? lastException = null;
        var attempts = Math.Max(0, _options.MaxRetries) + 1;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            try
            {
                using var response = await _httpClient.PostAsync(
                    "/api/v2/torrents/add",
                    new FormUrlEncodedContent(
                    [
                        new KeyValuePair<string, string>("urls", downloadUri.Trim()),
                    ]),
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    if (IsTransientStatusCode(response.StatusCode))
                    {
                        throw new HttpRequestException(
                            $"qBittorrent transient status code {(int)response.StatusCode}.");
                    }

                    throw new DownloadExecutionFailedException(
                        ProviderCode,
                        $"qBittorrent returned non-success status code {(int)response.StatusCode}.");
                }

                _logger.LogInformation(
                    "qBittorrent enqueue completed. Attempt={Attempt} DurationMs={DurationMs}",
                    attempt,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

                return new DownloadEnqueueResult(TryExtractBtih(downloadUri));
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
                    "qBittorrent enqueue transient failure. Attempt={Attempt}/{Attempts}",
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
                    "qBittorrent enqueue failed.",
                    exception);
            }
        }

        throw new DownloadExecutionUnavailableException(
            ProviderCode,
            "qBittorrent is unavailable after retry attempts.",
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
