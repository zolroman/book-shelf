using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookshelf.Infrastructure.Models;
using Bookshelf.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookshelf.Infrastructure.Services;

public sealed class QbittorrentDownloadClient(
    IHttpClientFactory httpClientFactory,
    IOptions<QbittorrentOptions> options,
    ILogger<QbittorrentDownloadClient> logger) : IQbittorrentDownloadClient
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, MockJobState> _mockJobs = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> EnqueueAsync(string downloadUri, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(downloadUri))
        {
            throw new ArgumentException("Download URI is required.", nameof(downloadUri));
        }

        var settings = options.Value;
        if (!settings.Enabled)
        {
            return EnqueueMock(downloadUri, settings);
        }

        try
        {
            var externalId = await EnqueueRealAsync(downloadUri, settings, cancellationToken);
            return externalId;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "qBittorrent enqueue failed.");
            if (!settings.UseMockFallback)
            {
                throw;
            }

            return EnqueueMock(downloadUri, settings);
        }
    }

    public async Task<ExternalDownloadStatus> GetStatusAsync(string externalJobId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalJobId))
        {
            return ExternalDownloadStatus.Unknown;
        }

        var settings = options.Value;

        var mockStatus = TryGetMockStatus(externalJobId, settings);
        if (mockStatus.HasValue)
        {
            return mockStatus.Value;
        }

        if (!settings.Enabled)
        {
            return ExternalDownloadStatus.Unknown;
        }

        try
        {
            return await GetRealStatusAsync(externalJobId, settings, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "qBittorrent status lookup failed for job {ExternalJobId}.", externalJobId);
            return ExternalDownloadStatus.Unknown;
        }
    }

    public async Task CancelAsync(string externalJobId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalJobId))
        {
            return;
        }

        var settings = options.Value;
        lock (_syncRoot)
        {
            if (_mockJobs.TryGetValue(externalJobId, out var mockJob))
            {
                mockJob.Canceled = true;
                return;
            }
        }

        if (!settings.Enabled)
        {
            return;
        }

        try
        {
            await CancelRealAsync(externalJobId, settings, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "qBittorrent cancel failed for job {ExternalJobId}.", externalJobId);
            if (!settings.UseMockFallback)
            {
                throw;
            }
        }
    }

    private string EnqueueMock(string downloadUri, QbittorrentOptions settings)
    {
        var externalId = MagnetUriHelper.TryExtractInfoHash(downloadUri) ?? $"mock-{Guid.NewGuid():N}";
        lock (_syncRoot)
        {
            _mockJobs[externalId] = new MockJobState
            {
                EnqueuedAtUtc = DateTime.UtcNow,
                AutoCompleteSeconds = Math.Max(1, settings.MockAutoCompleteSeconds)
            };
        }

        return externalId;
    }

    private ExternalDownloadStatus? TryGetMockStatus(string externalJobId, QbittorrentOptions settings)
    {
        lock (_syncRoot)
        {
            if (!_mockJobs.TryGetValue(externalJobId, out var state))
            {
                return null;
            }

            if (state.Canceled)
            {
                return ExternalDownloadStatus.Canceled;
            }

            var elapsed = DateTime.UtcNow - state.EnqueuedAtUtc;
            return elapsed.TotalSeconds >= Math.Max(1, state.AutoCompleteSeconds)
                ? ExternalDownloadStatus.Completed
                : ExternalDownloadStatus.Downloading;
        }
    }

    private async Task<string> EnqueueRealAsync(string downloadUri, QbittorrentOptions settings, CancellationToken cancellationToken)
    {
        return await ExecuteWithRetriesAsync(
            () => EnqueueRealOnceAsync(downloadUri, settings, cancellationToken),
            "enqueue",
            settings,
            cancellationToken);
    }

    private async Task<string> EnqueueRealOnceAsync(string downloadUri, QbittorrentOptions settings, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(QbittorrentDownloadClient));
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));
        client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");

        await LoginAsync(client, settings, cancellationToken);

        var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("urls", downloadUri)
        ]);

        using var response = await client.PostAsync("api/v2/torrents/add", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        return MagnetUriHelper.TryExtractInfoHash(downloadUri) ?? $"qb-{Guid.NewGuid():N}";
    }

    private async Task<ExternalDownloadStatus> GetRealStatusAsync(
        string externalJobId,
        QbittorrentOptions settings,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithRetriesAsync(
            () => GetRealStatusOnceAsync(externalJobId, settings, cancellationToken),
            "status",
            settings,
            cancellationToken);
    }

    private async Task<ExternalDownloadStatus> GetRealStatusOnceAsync(
        string externalJobId,
        QbittorrentOptions settings,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(QbittorrentDownloadClient));
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));
        client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");

        await LoginAsync(client, settings, cancellationToken);

        using var response = await client.GetAsync($"api/v2/torrents/info?hashes={Uri.EscapeDataString(externalJobId)}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<QbTorrentInfo>>(cancellationToken: cancellationToken) ?? [];
        var info = payload.FirstOrDefault();
        if (info is null)
        {
            return ExternalDownloadStatus.Unknown;
        }

        return info.State?.ToLowerInvariant() switch
        {
            "queueddl" or "stalleddl" => ExternalDownloadStatus.Queued,
            "downloading" or "metadl" => ExternalDownloadStatus.Downloading,
            "uploading" or "stalledup" or "pausedup" => ExternalDownloadStatus.Completed,
            "error" or "missingfiles" => ExternalDownloadStatus.Failed,
            _ => info.Progress >= 1 ? ExternalDownloadStatus.Completed : ExternalDownloadStatus.Downloading
        };
    }

    private async Task CancelRealAsync(string externalJobId, QbittorrentOptions settings, CancellationToken cancellationToken)
    {
        await ExecuteWithRetriesAsync(
            async () =>
            {
                await CancelRealOnceAsync(externalJobId, settings, cancellationToken);
                return true;
            },
            "cancel",
            settings,
            cancellationToken);
    }

    private async Task CancelRealOnceAsync(string externalJobId, QbittorrentOptions settings, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(QbittorrentDownloadClient));
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));
        client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");

        await LoginAsync(client, settings, cancellationToken);

        var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("hashes", externalJobId),
            new KeyValuePair<string, string>("deleteFiles", "false")
        ]);

        using var response = await client.PostAsync("api/v2/torrents/delete", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<T> ExecuteWithRetriesAsync<T>(
        Func<Task<T>> action,
        string operationName,
        QbittorrentOptions settings,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        var retries = Math.Max(0, settings.MaxRetries);

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action();
            }
            catch (Exception exception) when (attempt < retries && !cancellationToken.IsCancellationRequested)
            {
                lastException = exception;
                var delay = ComputeRetryDelay(settings.RetryDelayMilliseconds, attempt);
                logger.LogDebug(
                    exception,
                    "qBittorrent {Operation} retry {Attempt}/{TotalAttempts} in {DelayMs} ms.",
                    operationName,
                    attempt + 1,
                    retries + 1,
                    (int)delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception exception)
            {
                lastException = exception;
                break;
            }
        }

        throw lastException ?? new InvalidOperationException($"qBittorrent {operationName} failed.");
    }

    private static TimeSpan ComputeRetryDelay(int baseDelayMilliseconds, int attempt)
    {
        var normalizedBase = Math.Max(50, baseDelayMilliseconds);
        var exponential = normalizedBase * Math.Pow(2, attempt);
        var jitter = Random.Shared.Next(20, 120);
        var total = Math.Min(5_000, exponential + jitter);
        return TimeSpan.FromMilliseconds(total);
    }

    private static async Task LoginAsync(HttpClient client, QbittorrentOptions settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new InvalidOperationException("qBittorrent username/password are required when integration is enabled.");
        }

        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("username", settings.Username),
            new KeyValuePair<string, string>("password", settings.Password)
        ]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v2/auth/login")
        {
            Content = content
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
        if (!string.Equals(body, "Ok.", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("qBittorrent login failed.");
        }
    }

    private sealed class MockJobState
    {
        public DateTime EnqueuedAtUtc { get; init; }

        public int AutoCompleteSeconds { get; init; }

        public bool Canceled { get; set; }
    }

    private sealed record QbTorrentInfo(
        string? Hash,
        string? State,
        float Progress);
}
